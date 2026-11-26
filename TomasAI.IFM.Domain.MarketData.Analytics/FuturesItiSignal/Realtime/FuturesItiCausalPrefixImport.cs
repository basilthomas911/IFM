using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Explicit maintenance import of an audited missing causal prefix. Never reads current indicators.
/// The ordinary fenced owner, journal and admission path remain authoritative.</summary>
public static class FuturesItiCausalPrefixImport
{
    public sealed record FileProof(string Path, string Sha256);
    public sealed record SourceProof(FileProof Payload, long ArchiveArrivalUtcTicks);
    public sealed record Manifest(int Version, string Provenance, string Stream, long ExpectedCheckpointId,
        string ExpectedCheckpointSha256, Guid PreviousEpoch, long PreviousOrdinal, FileProof Successor,
        FileProof EpochStart, SourceProof Vx, FileProof AtrLineage, SourceProof[] Sources);

    const string Provenance = "ReconstructedCausalInputsFromEarlierPersistedAtrAndArchivedVx";
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task ApplyConfiguredAsync(FuturesItiResidentStore store, FuturesItiInputWal wal,
        Func<GenerateFuturesItiSignalCommand[], CancellationToken, Task<FuturesItiDurableAdmission>> prepare,
        CancellationToken token)
    {
        var path = Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_MANIFEST");
        var sha = Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_SHA256");
        if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(sha)) return;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sha))
            throw new InvalidDataException("Causal ITI repair requires both the manifest and its pinned SHA256.");
        await ApplyAsync(store, wal, new(path, sha), prepare, token).ConfigureAwait(false);
    }

    public static async Task ApplyAsync(FuturesItiResidentStore store, FuturesItiInputWal wal, FileProof proof,
        Func<GenerateFuturesItiSignalCommand[], CancellationToken, Task<FuturesItiDurableAdmission>> prepare,
        CancellationToken token = default)
    {
        store.AssertOwned();
        var manifestBytes = Read(proof, 1024 * 1024);
        var manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes, Json)
            ?? throw new InvalidDataException("Missing causal import manifest.");
        var inputs = Validate(manifest);
        var payloads = inputs.Select(FuturesItiJournalEnvelope.Encode).ToArray();
        // A completed audit permits only an exact idempotent replay of these original frozen bytes.
        var auditDirectory = Path.Combine(wal.DirectoryPath, "causal-recovery");
        var auditPath = Path.Combine(auditDirectory, proof.Sha256.ToLowerInvariant() + ".json");
        var audited = false;
        if (File.Exists(auditPath))
        {
            using var audit = JsonDocument.Parse(File.ReadAllBytes(auditPath));
            audited = audit.RootElement.GetProperty("ManifestSha256").GetString() == Hash(manifestBytes)
                && audit.RootElement.GetProperty("RootId").GetGuid() == wal.RootId
                && audit.RootElement.GetProperty("PayloadSha256").EnumerateArray().Select(value => value.GetString()).SequenceEqual(payloads.Select(Hash));
            if (!audited) throw new InvalidDataException("Causal import audit differs from the owned immutable inputs.");
        }
        var checkpoints = await store.ReadCheckpointInputsAsync(token).ConfigureAwait(false);
        var anchor = checkpoints.SingleOrDefault(row => row.Stream == manifest.Stream)
            ?? throw new InvalidDataException("Causal import requires its original committed predecessor.");
        var anchorCommand = FuturesItiJournalEnvelope.Decode(anchor.Payload, anchor.Id);
        var exactPredecessor = anchor.Id == manifest.ExpectedCheckpointId
            && Hash(anchor.Payload).Equals(manifest.ExpectedCheckpointSha256, StringComparison.OrdinalIgnoreCase)
            && anchorCommand.SourceCursor is { } previous && previous.Epoch == manifest.PreviousEpoch
            && previous.Ordinal == manifest.PreviousOrdinal;
        if (!exactPredecessor && !(audited && anchorCommand.SourceCursor is { } current
            && current.Epoch == inputs[0].SourceCursor!.Epoch && current.Ordinal >= 5))
            throw new InvalidDataException("Causal import predecessor differs; no cursor is reset or replaced.");
        if (!exactPredecessor)
        {
            // The normal append checks the original PG receipt bytes, even if the already-settled WAL was reclaimed.
            for (var index = 0; index < inputs.Length; index++) inputs[index] = MessageOrigins.Copy(inputs[index], inputs[index] with { JournalPayload = payloads[index] });
            _ = await prepare(inputs, token).ConfigureAwait(false);
            return;
        }

        var retained = new Dictionary<Guid, byte[]>();
        var successorPayload = Read(manifest.Successor, 65536);
        var successorFound = false;
        await foreach (var row in wal.ReplayAsync(token).ConfigureAwait(false))
        {
            var command = FuturesItiJournalEnvelope.Decode(row.Payload, 0);
            if (command.StreamId == manifest.Stream && command.SourceCursor is { } cursor && cursor.Epoch == inputs[0].SourceCursor!.Epoch)
            {
                if (cursor.Ordinal == 6)
                {
                    if (!row.Payload.AsSpan().SequenceEqual(successorPayload)) throw new InvalidDataException("Owned source-six successor differs from its original pinned bytes.");
                    successorFound = true;
                }
                if (cursor.Ordinal is >= 1 and <= 5 && !row.Payload.AsSpan().SequenceEqual(payloads[(int)cursor.Ordinal - 1]))
                    throw new InvalidDataException("Owned prefix cursor already has different immutable command bytes.");
            }
            if (inputs.Any(input => input.CommandId == command.CommandId))
            {
                if (retained.TryGetValue(command.CommandId, out var old) && !old.AsSpan().SequenceEqual(row.Payload))
                    throw new InvalidDataException("Conflicting retained causal repair command.");
                retained[command.CommandId] = row.Payload;
            }
        }
        if (!successorFound) throw new InvalidDataException("The authentic source-six successor is not present in this owned WAL.");
        var missing = new List<byte[]>();
        for (var index = 0; index < inputs.Length; index++)
        {
            if (retained.TryGetValue(inputs[index].CommandId, out var bytes))
            {
                if (!bytes.AsSpan().SequenceEqual(payloads[index])) throw new InvalidDataException("Causal import changed an existing input.");
            }
            else missing.Add(payloads[index]);
        }
        if (missing.Count > 0) await wal.AppendBatchAsync(missing, token).ConfigureAwait(false);
        // Prepare BEFORE ordinary WAL/PG recovery: SQL orders the late-inserted prefix by source ordinal.
        for (var index = 0; index < inputs.Length; index++) inputs[index] = MessageOrigins.Copy(inputs[index], inputs[index] with { JournalPayload = payloads[index] });
        _ = await prepare(inputs, token).ConfigureAwait(false);
        Directory.CreateDirectory(auditDirectory);
        if (!File.Exists(auditPath))
        {
            var audit = JsonSerializer.SerializeToUtf8Bytes(new { Version = 1, ManifestSha256 = Hash(manifestBytes),
                manifest.Provenance, wal.RootId, manifest.Stream, manifest.ExpectedCheckpointId,
                SourceIds = manifest.Sources.Select(source => MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(Read(source.Payload, 65536)).Id),
                CommandIds = inputs.Select(input => input.CommandId), PayloadSha256 = payloads.Select(Hash),
                AppliedAtUtc = DateTimeOffset.UtcNow, OriginalHotCacheAvailabilityClaimed = false });
            var temporary = auditPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var writer = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            { writer.Write(audit); writer.Flush(true); }
            File.Move(temporary, auditPath, overwrite: false);
        }
    }

    public static GenerateFuturesItiSignalCommand[] Validate(Manifest manifest)
    {
        if (manifest.Version != 1 || manifest.Provenance != Provenance || manifest.Sources.Length != 5)
            throw new InvalidDataException("Only an explicit audited five-source causal prefix is supported.");
        var successor = FuturesItiJournalEnvelope.Decode(Read(manifest.Successor, 65536), 0);
        if (successor.StreamId != manifest.Stream || successor.SourceCursor is not { Ordinal: 6 } next
            || !successor.InputsFrozen || successor.DailyAtr is not { } atr || !successor.RequireDailyAtr
            || successor.TimePeriod != TimeFrameType.Daily || successor.InputUnavailableReason is not null)
            throw new InvalidDataException("Causal import requires the unchanged frozen source-six successor.");
        var checkpoint = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(Read(manifest.EpochStart, 65536));
        if (checkpoint.EntityId.ContractId != successor.ContractId || checkpoint.EntityId.ValueDate != successor.ValueDate
            || checkpoint.VwapCheckpoint is not { IsReplayComplete: true, LastTradeOrdinal: 0 } cp || cp.StreamEpochId != next.Epoch)
            throw new InvalidDataException("Missing original zero-ordinal epoch-start authority.");
        var sources = manifest.Sources.Select(source => MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(Read(source.Payload, 65536))).ToArray();
        var vx = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(Read(manifest.Vx.Payload, 65536));
        if (!vx.EntityId.ContractId.StartsWith("VX", StringComparison.Ordinal) || vx.Price.Trade is not { } vxTrade
            || (double)vxTrade.LastPrice != successor.VixFuturesPrice
            || manifest.Vx.ArchiveArrivalUtcTicks >= manifest.Sources.Min(source => source.ArchiveArrivalUtcTicks))
            throw new InvalidDataException("VX evidence is absent, different or not archived before the missing prefix.");
        using var lineage = JsonDocument.Parse(Read(manifest.AtrLineage, 1024 * 1024));
        var root = lineage.RootElement;
        var persisted = root.GetProperty("actualSourceEvent").GetProperty("eventTimestamp").GetDateTimeOffset().UtcDateTime;
        var metadata = root.GetProperty("signal").GetProperty("metadata");
        if (root.GetProperty("contract").GetString() != successor.ContractId || root.GetProperty("period").GetInt32() != 14
            || root.GetProperty("signal").GetProperty("atr").GetDouble() != atr.Value
            || metadata[6].GetInt64() != atr.SourceSequence
            || DateTimeOffset.Parse(metadata[4][0].GetProperty("utc").GetString()!, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime != atr.ObservedAtUtc
            || DateTimeOffset.Parse(metadata[5][0].GetProperty("utc").GetString()!, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime != atr.CalculatedAtUtc)
            throw new InvalidDataException("Earlier persisted ATR proof differs from the original frozen successor.");
        var result = new GenerateFuturesItiSignalCommand[5];
        for (var index = 0; index < sources.Length; index++)
        {
            var source = sources[index];
            var trade = source.Price.Trade ?? throw new InvalidDataException("Missing original trade.");
            if (source.EntityId.ContractId != successor.ContractId || source.Price.ContractId != successor.ContractId
                || source.EntityId.ValueDate != successor.ValueDate || source.Price.ValueDate != successor.ValueDate
                || trade.StreamEpochId != next.Epoch || trade.TradeOrdinal != index + 1
                || source.SourceTrade?.Dataset != next.Dataset || source.Price.PublisherId != next.PublisherId || source.Price.InstrumentId != next.InstrumentId
                || trade.EventTimestamp.UtcDateTime >= next.EventAtUtc || persisted >= trade.EventTimestamp.UtcDateTime
                || vxTrade.EventTimestamp.UtcDateTime >= trade.EventTimestamp.UtcDateTime || !atr.IsUsableAt(successor.ContractId, trade.EventTimestamp.UtcDateTime))
                throw new InvalidDataException($"Original prefix identity, ordering or causal input time differs: ordinal={trade.TradeOrdinal}; entity={source.EntityId}; priceContract={source.Price.ContractId}; date={source.Price.ValueDate}; dataset='{source.SourceDataset}'/'{next.Dataset}'; publisher={source.Price.PublisherId}/{next.PublisherId}; instrument={source.Price.InstrumentId}/{next.InstrumentId}; epoch={trade.StreamEpochId}/{next.Epoch}; event={trade.EventTimestamp:O}; successor={next.EventAtUtc:O}; persisted={persisted:O}; vx={vxTrade.EventTimestamp:O}; ATRusable={atr.IsUsableAt(successor.ContractId, trade.EventTimestamp.UtcDateTime)}.");
            var cursor = new FuturesItiSourceCursor(next.Dataset, next.PublisherId, next.InstrumentId, next.Epoch,
                trade.TradeOrdinal, trade.SourceSequence, trade.EventTimestamp.UtcDateTime, trade.ReceiveTimestamp.UtcDateTime);
            var identity = $"ITI|{source.Price.ContractId}|{source.Price.ValueDate:yyyyMMdd}|{cursor.Dataset}|{cursor.PublisherId}|{cursor.InstrumentId}|{cursor.Epoch:N}|{cursor.Ordinal}";
            var command = successor with { CommandId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16)),
                Timestamp = trade.EventTimestamp.UtcDateTime, FuturesPrice = (double)trade.LastPrice, SourceCursor = cursor,
                VerifiedEpochStart = index == 0, SourceCaptureRequired = false, SourceEventId = source.Id,
                JournalPayload = null, JournalSequence = 0, ResidentAdmission = null, ResidentBatch = null };
            result[index] = MessageOrigins.Restore(command, new MessageOrigin(new DateTimeOffset(command.Timestamp, TimeSpan.Zero), "system", null, null, source.Id));
        }
        return result;
    }

    static byte[] Read(FileProof proof, int maximum)
    {
        if (!Path.IsPathFullyQualified(proof.Path) || proof.Sha256.Length != 64 || !proof.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Causal evidence requires an absolute path and SHA256.");
        var info = new FileInfo(proof.Path);
        if (info.Length is < 1 || info.Length > maximum) throw new InvalidDataException("Causal evidence exceeds its bound.");
        var bytes = File.ReadAllBytes(proof.Path);
        if (!Hash(bytes).Equals(proof.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Causal evidence SHA256 differs.");
        return bytes;
    }
    static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

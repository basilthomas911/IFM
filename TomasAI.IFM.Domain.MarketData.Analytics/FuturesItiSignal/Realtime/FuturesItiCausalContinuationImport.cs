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
using FileProof = TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.FuturesItiCausalPrefixImport.FileProof;
using SourceProof = TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.FuturesItiCausalPrefixImport.SourceProof;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

/// <summary>Explicit one-input maintenance repair from an earlier frozen adjacent predecessor, never current caches.</summary>
public static class FuturesItiCausalContinuationImport
{
    public sealed record Manifest(int Version, string Provenance, string Stream, long ExpectedCheckpointId,
        FileProof FrozenPredecessor, SourceProof OriginalPredecessor, SourceProof OriginalContinuation);
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public static async Task ApplyConfiguredAsync(FuturesItiResidentStore store, FuturesItiInputWal wal,
        Func<GenerateFuturesItiSignalCommand[], CancellationToken, Task<FuturesItiDurableAdmission>> prepare, CancellationToken token)
    {
        var path = Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_MANIFEST");
        var sha = Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_SHA256");
        if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(sha)) return;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sha)) throw new InvalidDataException("Both continuation manifest and pinned SHA256 are required.");
        await ApplyAsync(store, wal, new(path, sha), prepare, token).ConfigureAwait(false);
    }
    public static async Task ApplyAsync(FuturesItiResidentStore store, FuturesItiInputWal wal, FileProof proof,
        Func<GenerateFuturesItiSignalCommand[], CancellationToken, Task<FuturesItiDurableAdmission>> prepare, CancellationToken token=default)
    {
        store.AssertOwned(); var manifestBytes = Read(proof, 1024 * 1024);
        var manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes, Json) ?? throw new InvalidDataException("Missing continuation manifest.");
        var input = Validate(manifest); var payload = FuturesItiJournalEnvelope.Encode(input);
        var auditDirectory = Path.Combine(wal.DirectoryPath, "causal-continuation-recovery");
        var auditPath = Path.Combine(auditDirectory, Hash(manifestBytes) + ".json");
        var checkpoints = await store.ReadCheckpointInputsAsync(token).ConfigureAwait(false);
        var anchor = checkpoints.SingleOrDefault(row => row.Stream == manifest.Stream) ?? throw new InvalidDataException("Original continuation predecessor is absent.");
        var exact = anchor.Id == manifest.ExpectedCheckpointId && Hash(anchor.Payload).Equals(manifest.FrozenPredecessor.Sha256, StringComparison.OrdinalIgnoreCase);
        if (!exact)
        {
            if (!File.Exists(auditPath)) throw new InvalidDataException("Continuation repair predecessor changed; no cursor will be reset.");
            using var audit = JsonDocument.Parse(File.ReadAllBytes(auditPath));
            if (audit.RootElement.GetProperty("ManifestSha256").GetString() != Hash(manifestBytes)
                || audit.RootElement.GetProperty("RootId").GetGuid() != wal.RootId
                || audit.RootElement.GetProperty("PayloadSha256").GetString() != Hash(payload))
                throw new InvalidDataException("Continuation audit differs from the immutable repaired input.");
            var completed = await store.ReadOriginalSourceAsync(manifest.Stream, input.SourceCursor!.Epoch, input.SourceCursor.Ordinal, token).ConfigureAwait(false);
            if (completed.Count != 1 || !completed[0].Payload.AsSpan().SequenceEqual(payload))
                throw new InvalidDataException("Completed continuation has no exact original durable input.");
            return;
        }
        var exists = false;
        await foreach (var row in wal.ReplayAsync(token).ConfigureAwait(false))
        {
            var retained = FuturesItiJournalEnvelope.Decode(row.Payload, 0);
            if (retained.CommandId == input.CommandId || retained.StreamId == manifest.Stream
                && retained.SourceCursor is{} cursor && cursor.Epoch == input.SourceCursor!.Epoch && cursor.Ordinal == input.SourceCursor.Ordinal)
            {
                if (!row.Payload.AsSpan().SequenceEqual(payload)) throw new InvalidDataException("Original continuation cursor has conflicting frozen bytes.");
                exists = true;
            }
        }
        if (!exists) await wal.AppendAsync(payload, token).ConfigureAwait(false);
        input = MessageOrigins.Copy(input, input with { JournalPayload = payload });
        _ = await prepare([input], token).ConfigureAwait(false);
        Directory.CreateDirectory(auditDirectory);
        if (!File.Exists(auditPath))
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Version=2, ManifestSha256=Hash(manifestBytes), wal.RootId,
                manifest.Provenance, manifest.ExpectedCheckpointId, OriginalSourceId=input.SourceEventId, input.CommandId,
                PayloadSha256=Hash(payload), AppliedAtUtc=DateTimeOffset.UtcNow, OriginalHotCacheAvailabilityClaimed=false });
            var temporary=auditPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var writer=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough)){writer.Write(bytes);writer.Flush(true);}
            File.Move(temporary,auditPath,false);
        }
    }
    public static GenerateFuturesItiSignalCommand Validate(Manifest manifest)
    {
        if(manifest.Version!=2||manifest.Provenance!="ReconstructedCausalInputFromEarlierFrozenPredecessorAtrAndVx")
            throw new InvalidDataException("Unsupported explicit continuation provenance.");
        var prior=FuturesItiJournalEnvelope.Decode(Read(manifest.FrozenPredecessor,65536),0);
        var old=MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(Read(manifest.OriginalPredecessor.Payload,65536));
        var source=MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(Read(manifest.OriginalContinuation.Payload,65536));
        if(prior.StreamId!=manifest.Stream||!prior.InputsFrozen||prior.InputUnavailableReason is not null||prior.SourceCursor is not{} previous
            ||prior.TimePeriod!=TimeFrameType.Daily||!prior.RequireDailyAtr||prior.DailyAtr is not{} atr||!double.IsFinite(prior.VixFuturesPrice)||prior.VixFuturesPrice<=0
            ||old.Price.Trade is not{} oldTrade||source.Price.Trade is not{} trade
            ||old.VwapCheckpoint is not null||source.VwapCheckpoint is not null
            ||old.SourceTrade?.Dataset!=previous.Dataset||source.SourceTrade?.Dataset!=previous.Dataset
            ||old.Price.ContractId!=prior.ContractId||source.Price.ContractId!=prior.ContractId
            ||old.EntityId.ContractId!=prior.ContractId||source.EntityId.ContractId!=prior.ContractId
            ||old.Price.ValueDate!=prior.ValueDate||source.Price.ValueDate!=prior.ValueDate||source.EntityId.ValueDate!=prior.ValueDate
            ||old.Price.PublisherId!=previous.PublisherId||source.Price.PublisherId!=previous.PublisherId
            ||old.Price.InstrumentId!=previous.InstrumentId||source.Price.InstrumentId!=previous.InstrumentId
            ||oldTrade.StreamEpochId!=previous.Epoch||trade.StreamEpochId!=previous.Epoch
            ||oldTrade.TradeOrdinal!=previous.Ordinal||trade.TradeOrdinal!=previous.Ordinal+1
            ||oldTrade.SourceSequence!=previous.SourceSequence||oldTrade.EventTimestamp.UtcDateTime!=previous.EventAtUtc
            ||oldTrade.ReceiveTimestamp.UtcDateTime!=previous.ReceivedAtUtc||prior.Timestamp!=previous.EventAtUtc||prior.FuturesPrice!=(double)oldTrade.LastPrice
            ||trade.EventTimestamp.UtcDateTime<=previous.EventAtUtc||manifest.OriginalPredecessor.ArchiveArrivalUtcTicks>manifest.OriginalContinuation.ArchiveArrivalUtcTicks
            ||!atr.IsUsableAt(prior.ContractId,trade.EventTimestamp.UtcDateTime))
            throw new InvalidDataException("Original adjacent continuation or earlier frozen causal inputs differ.");
        var cursor=previous with{Ordinal=trade.TradeOrdinal,SourceSequence=trade.SourceSequence,EventAtUtc=trade.EventTimestamp.UtcDateTime,ReceivedAtUtc=trade.ReceiveTimestamp.UtcDateTime};
        var identity=$"ITI|{prior.ContractId}|{prior.ValueDate:yyyyMMdd}|{cursor.Dataset}|{cursor.PublisherId}|{cursor.InstrumentId}|{cursor.Epoch:N}|{cursor.Ordinal}";
        var input=prior with{CommandId=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0,16)),Timestamp=cursor.EventAtUtc,
            FuturesPrice=(double)trade.LastPrice,SourceCursor=cursor,SourceEventId=source.Id,SourceCaptureRequired=false,VerifiedEpochStart=false,
            JournalPayload=null,JournalSequence=0,ResidentAdmission=null,ResidentBatch=null};
        return MessageOrigins.Restore(input,new MessageOrigin(new DateTimeOffset(input.Timestamp,TimeSpan.Zero),"system",null,null,source.Id));
    }
    static byte[] Read(FileProof proof,int maximum)
    {
        if(!Path.IsPathFullyQualified(proof.Path)||proof.Sha256.Length!=64||!proof.Sha256.All(Uri.IsHexDigit))throw new InvalidDataException("Continuation proof must be absolute and SHA256 pinned.");
        var info=new FileInfo(proof.Path);if(info.Length is<1||info.Length>maximum)throw new InvalidDataException("Continuation proof exceeds its bound.");
        var bytes=File.ReadAllBytes(proof.Path);if(!Hash(bytes).Equals(proof.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Continuation proof SHA256 differs.");return bytes;
    }
    static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

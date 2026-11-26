using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

public partial class FuturesItiStorageBaselineBenchmarks
{
    public async Task VerifyCausalContinuationImportAsync(string manifestPath)
    {
        qualifying=true;
        var manifest=JsonSerializer.Deserialize<FuturesItiCausalContinuationImport.Manifest>(await File.ReadAllBytesAsync(manifestPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var continuation=FuturesItiCausalContinuationImport.Validate(manifest);
        var original=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent>(await File.ReadAllBytesAsync(manifest.OriginalContinuation.Payload.Path));
        var predecessor=FuturesItiJournalEnvelope.Decode(await File.ReadAllBytesAsync(manifest.FrozenPredecessor.Path),0);
        await actor.ProcessPreparedAsync(await actor.PrepareInputsAsync([predecessor]));
        await actor.SealProcessedEpochsAsync();
        Require(!await actor.ProveProcessedOriginalAsync(original),"Unprocessed continuation falsely proved completed.");
        var checkpoint=(await actor.ResidentStore!.ReadCheckpointInputsAsync()).Single(row=>row.Stream==manifest.Stream);
        manifest=manifest with{ExpectedCheckpointId=checkpoint.Id};
        var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest);var path=Path.Combine(fixtureJournalDirectory,"continuation.json");await File.WriteAllBytesAsync(path,bytes);
        var proof=new FuturesItiCausalPrefixImport.FileProof(path,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        await FuturesItiCausalContinuationImport.ApplyAsync(actor.ResidentStore,actor.InputWal!,proof,actor.PrepareInputsAsync);
        var before=actor.InputWal!.RetainedAndReservedBytes;
        await FuturesItiCausalContinuationImport.ApplyAsync(actor.ResidentStore,actor.InputWal,proof,actor.PrepareInputsAsync);
        Require(actor.InputWal.RetainedAndReservedBytes==before,"Continuation retry duplicated original WAL bytes.");
        var oldPath=Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_MANIFEST");var oldSha=Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_SHA256");
        try
        {
            Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_MANIFEST",path);Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_SHA256",proof.Sha256);
            await actor.StopAsync();actor=CreateActor();await actor.StartAsync(database.Supervisor);
            Require(await actor.ProveProcessedOriginalAsync(original),"Recovered authentic original has no positive committed proof.");
            var changed=original with{Price=original.Price with{Trade=original.Price.Trade!.Value with{LastPrice=original.Price.Trade.Value.LastPrice+1}}};
            var rejected=false;try{_ = await actor.ProveProcessedOriginalAsync(changed);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"Changed original price passed committed cursor proof.");
            var events=await database.EventCountAsync(manifest.Stream);
            await actor.StopAsync();actor=CreateActor();await actor.StartAsync(database.Supervisor);
            Require(await database.EventCountAsync(manifest.Stream)==events,"Continuation restart duplicated committed events.");
            Console.WriteLine("CAUSAL_CONTINUATION_IMPORT_PASS: actual predecessor owner hash; authentic archived adjacent195; earlier frozen194 ATR/VX only; no current cache; WAL/PrepareInputs idempotence; startup recovery twice; unprocessed proof false; committed exact proof true; changed price rejected. Disposable actor/PG, not live activation.");
        }
        finally{Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_MANIFEST",oldPath);Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_CONTINUATION_SHA256",oldSha);}
    }
    /// <summary>Actual fenced resident actor on disposable PostgreSQL; authentic source fixtures, synthetic predecessor state.</summary>
    public async Task VerifyCausalPrefixImportAsync(string manifestPath)
    {
        qualifying = true;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var original = JsonSerializer.Deserialize<FuturesItiCausalPrefixImport.Manifest>(await File.ReadAllBytesAsync(manifestPath), options)!;
        var inputs = FuturesItiCausalPrefixImport.Validate(original);
        var successorPayload = await File.ReadAllBytesAsync(original.Successor.Path);
        var successor = FuturesItiJournalEnvelope.Decode(successorPayload, 0);
        var predecessor = successor with { CommandId = Guid.NewGuid(), VerifiedEpochStart = false,
            Timestamp = inputs[0].Timestamp.AddSeconds(-1),
            SourceCursor = successor.SourceCursor! with { Epoch = original.PreviousEpoch, Ordinal = original.PreviousOrdinal,
                EventAtUtc = inputs[0].Timestamp.AddSeconds(-1), ReceivedAtUtc = inputs[0].Timestamp.AddSeconds(-1) },
            JournalPayload = null, JournalSequence = 0, InputUnavailableReason = "Synthetic disposable predecessor anchor; authentic lost source fixtures follow." };
        await actor.ProcessPreparedAsync(await actor.PrepareInputsAsync([predecessor]));
        await actor.SealProcessedEpochsAsync();
        var checkpoint = (await actor.ResidentStore!.ReadCheckpointInputsAsync()).Single(row => row.Stream == original.Stream);
        var manifest = original with { ExpectedCheckpointId = checkpoint.Id,
            ExpectedCheckpointSha256 = Convert.ToHexStringLower(SHA256.HashData(checkpoint.Payload)) };
        var path = Path.Combine(fixtureJournalDirectory, "disposable-causal-import.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        await File.WriteAllBytesAsync(path, bytes);
        var proof = new FuturesItiCausalPrefixImport.FileProof(path, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var beforeMissing = actor.InputWal!.RetainedAndReservedBytes;
        var missingRejected = false;
        try { await FuturesItiCausalPrefixImport.ApplyAsync(actor.ResidentStore, actor.InputWal, proof, actor.PrepareInputsAsync); }
        catch (InvalidDataException) { missingRejected = true; }
        Require(missingRejected && actor.InputWal.RetainedAndReservedBytes == beforeMissing,
            "Missing owned successor was accepted or changed the WAL.");
        // Save the unchanged successor FIRST, reproducing the real failed source-six ownership order.
        await actor.InputWal.AppendAsync(successorPayload, default);
        _ = await actor.PrepareInputsAsync([successor]);
        await FuturesItiCausalPrefixImport.ApplyAsync(actor.ResidentStore, actor.InputWal, proof, actor.PrepareInputsAsync);
        var pending = new List<GenerateFuturesItiSignalCommand>();
        await foreach (var row in actor.ResidentStore.ReadPendingAsync())
            if (row.Stream == manifest.Stream) pending.Add(FuturesItiJournalEnvelope.Decode(row.Payload, row.Id));
        Require(pending.Select(input => input.SourceCursor!.Ordinal).SequenceEqual(new long[] { 1, 2, 3, 4, 5, 6 }),
            "Late prefix was not ordered before the already-owned source-six input.");
        Require(pending[^1].JournalPayload!.AsSpan().SequenceEqual(successorPayload), "Original successor bytes changed.");
        var before = actor.InputWal.RetainedAndReservedBytes;
        await FuturesItiCausalPrefixImport.ApplyAsync(actor.ResidentStore, actor.InputWal, proof, actor.PrepareInputsAsync);
        Require(before == actor.InputWal.RetainedAndReservedBytes, "Idempotent import appended duplicate WAL records.");
        var rejected = false;
        try { FuturesItiCausalPrefixImport.Validate(manifest with { Sources = manifest.Sources.Reverse().ToArray() }); }
        catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Reordered original evidence was accepted.");
        var originalPath = Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_MANIFEST");
        var originalSha = Environment.GetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_SHA256");
        try
        {
            Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_MANIFEST", path);
            Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_SHA256", proof.Sha256);
            await actor.StopAsync();
            actor = CreateActor();
            await actor.StartAsync(database.Supervisor);
            var anchor = (await actor.ResidentStore!.ReadCheckpointInputsAsync()).Single(row => row.Stream == manifest.Stream);
            Require(FuturesItiJournalEnvelope.Decode(anchor.Payload, anchor.Id).SourceCursor!.Ordinal == 6,
                "Normal startup did not recover the contiguous prefix and successor.");
            var eventCount = await database.EventCountAsync(manifest.Stream);
            await actor.StopAsync(); actor = CreateActor(); await actor.StartAsync(database.Supervisor);
            Require(await database.EventCountAsync(manifest.Stream) == eventCount, "Restart duplicated a committed signal.");
            Console.WriteLine("CAUSAL_PREFIX_IMPORT_PASS: authentic source1..5 and cp0; earlier persisted ATR/archived VX; source6 byte-exact; normal fenced WAL/PrepareInputs; late-ID SQL order1..6; idempotent import; changed order rejected; two normal startup recoveries without duplicate signals. Disposable predecessor state only; not live activation.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_MANIFEST", originalPath);
            Environment.SetEnvironmentVariable("IFM_ITI_CAUSAL_PREFIX_SHA256", originalSha);
        }
    }
}

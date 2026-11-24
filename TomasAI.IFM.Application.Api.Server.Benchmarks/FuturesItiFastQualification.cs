using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

public partial class FuturesItiStorageBaselineBenchmarks
{
    public async Task VerifyFastAsync()
    {
        qualifying = true;
        var store = actor.ResidentStore!;
        var noChanges = Enumerable.Range(0, 128).Select(_ => SourceCommand(5000)).ToArray();
        var receipt = await actor.PrepareInputsAsync(noChanges).ConfigureAwait(false);
        var transactions = store.Transactions;
        var auditCalls = audits.Calls;
        var beforeLoads = loads;
        await actor.ProcessPreparedAsync(receipt).ConfigureAwait(false);
        Require(store.Transactions == transactions && audits.Calls == auditCalls && loads == beforeLoads && counted.Guards == 0,
            "Warm no-change processing touched database/audit/stream head.");
        await actor.ProcessPreparedAsync(receipt).ConfigureAwait(false);
        Require(store.Transactions == transactions, "Duplicate no-change delivery touched storage.");

        var reference = new FuturesItiSignalCommandState();
        Require((Command(5000) with { Timestamp = epoch.AddTicks(1) }).Execute(reference).Success, "Reference seed failed.");
        reference.AcceptChanges();
        var changes = new[] { 6000d, 4000, 6500, 3900, 6600, 3800 }.Select(SourceCommand).ToArray();
        var expected = new List<byte[]>();
        foreach (var input in changes)
        {
            Require(input.Execute(reference).Success, "Reference rejected durable input.");
            expected.AddRange(reference.Events.OfType<FuturesItiSignalGeneratedEvent>()
                .Select(value => MessagePack.MessagePackSerializer.Serialize(value.FuturesItiSignal)));
            reference.AcceptChanges();
        }
        counted.Capture = true;
        counted.Captured.Clear();
        var changing = await actor.PrepareInputsAsync(changes).ConfigureAwait(false);
        await actor.ProcessPreparedAsync(changing).ConfigureAwait(false);
        Require(counted.Captured.Count == expected.Count && counted.Captured.Zip(expected)
            .All(pair => pair.First.AsSpan().SequenceEqual(pair.Second)), "Batch changed evaluator output or frozen context.");
        var stream = changes[0].StreamId;
        var count = await database.EventCountAsync(stream).ConfigureAwait(false);
        await actor.ProcessPreparedAsync(changing).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Duplicate batch appended another event.");

        var fenced = false;
        try { await using var competing = await FuturesItiResidentStore.OpenAsync(database.Events).ConfigureAwait(false); }
        catch (InvalidOperationException) { fenced = true; }
        Require(fenced, "A second owner acquired the database fence.");

        // Crash before decision: only the durable inbox exists. Recreating the owner must consume it exactly once.
        var pending = new[] { SourceCommand(6700), SourceCommand(3700) };
        _ = await actor.PrepareInputsAsync(pending).ConfigureAwait(false);
        await actor.StopAsync().ConfigureAwait(false);
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count + 2, "Restart did not recover journaled inputs.");
        count += 2;
        await actor.StopAsync().ConfigureAwait(false);
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Restart duplicated committed inputs.");

        // Deferred PostgreSQL failure happens at COMMIT, after source/projection/checkpoint SQL ran.
        await using (var connection = await database.OpenAsync().ConfigureAwait(false))
        {
            await using var trigger = new NpgsqlCommand("""
                CREATE FUNCTION reject_iti_commit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'qualification rollback'; END $$;
                CREATE CONSTRAINT TRIGGER reject_iti_commit AFTER INSERT OR UPDATE ON futures_iti_input_checkpoint
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reject_iti_commit();
                """, connection);
            _ = await trigger.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        var rollback = await actor.PrepareInputsAsync([SourceCommand(6800)]).ConfigureAwait(false);
        var rejected = false;
        try { await actor.ProcessPreparedAsync(rollback).ConfigureAwait(false); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && await database.EventCountAsync(stream).ConfigureAwait(false) == count,
            "Failed commit left an event or acknowledged candidate state.");
        await using (var connection = await database.OpenAsync().ConfigureAwait(false))
        {
            await using var drop = new NpgsqlCommand("DROP TRIGGER reject_iti_commit ON futures_iti_input_checkpoint; DROP FUNCTION reject_iti_commit();", connection);
            _ = await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await actor.StopAsync().ConfigureAwait(false);
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count + 1, "Failed durable input was not recovered exactly once.");
        count++;

        // Gap is persisted but never silently skipped. Authentic late arrival repairs the source order on restart.
        var missing = SourceCommand(3600);
        var afterGap = SourceCommand(6900);
        var gap = await actor.PrepareInputsAsync([afterGap]).ConfigureAwait(false);
        rejected = false;
        try { await actor.ProcessPreparedAsync(gap).ConfigureAwait(false); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Source gap was not blocked.");
        _ = await actor.PrepareInputsAsync([missing]).ConfigureAwait(false);
        await actor.StopAsync().ConfigureAwait(false);
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count + 2, "Authentic late arrival did not restore source order.");
        count += 2;

        var missingContext = SourceCommand(5000) with { VixFuturesPrice = 0, InputUnavailableReason = "Missing authentic VX input." };
        await actor.ProcessPreparedAsync(await actor.PrepareInputsAsync([missingContext]).ConfigureAwait(false)).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Unavailable context manufactured a signal.");

        // Explicitly sealed intake + completed provider replay permits a new live epoch at ordinal one.
        await actor.SealProcessedEpochsAsync().ConfigureAwait(false);
        sourceEpoch = Guid.NewGuid();
        sourceOrdinal = 0;
        var successor = SourceCommand(7000) with { VerifiedEpochStart = true };
        await actor.ProcessPreparedAsync(await actor.PrepareInputsAsync([successor]).ConfigureAwait(false)).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count + 1, "Sealed epoch handover did not continue.");
        count++;
        await actor.ProcessPreparedAsync(await actor.PrepareInputsAsync(changes).ConfigureAwait(false)).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Old WAL prefix replayed a retired epoch.");
        await actor.StopAsync().ConfigureAwait(false);
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Epoch bridge did not survive restart.");

        // The fence is a real server session, not a local always-successful flag.
        var afterLoss = await actor.PrepareInputsAsync([SourceCommand(7000)]).ConfigureAwait(false);
        await using (var connection = await database.OpenAsync().ConfigureAwait(false))
        {
            await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend($1)", connection);
            terminate.Parameters.AddWithValue(actor.ResidentStore!.BackendProcessId);
            _ = await terminate.ExecuteScalarAsync().ConfigureAwait(false);
        }
        await Task.Delay(1500).ConfigureAwait(false);
        rejected = false;
        try { await actor.ProcessPreparedAsync(afterLoss).ConfigureAwait(false); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Lost writer session continued acknowledging inputs.");
        await actor.StopAsync().ConfigureAwait(false);
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count, "Lease recovery duplicated an event.");

        // Crash before even the PostgreSQL inbox write: the local WAL must be imported before source recovery.
        var walOnly = SourceCommand(3500);
        MessageOrigins.ForPublication(walOnly, new TomasAI.IFM.TestSupport.ResidentActors.ResidentPilotClock(epoch.AddSeconds(2)));
        _ = await actor.InputWal!.AppendAsync(FuturesItiJournalEnvelope.Encode(walOnly), default).ConfigureAwait(false);
        counted.Captured.Clear();
        counted.CapturedEvents.Clear();
        await actor.StopAsync().ConfigureAwait(false);
        RegimeDiscoverySignalCacheAdapter.TryGetLatestAtr(Contract,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.FiveMinutes, out var oldAtr);
        RegimeDiscoverySignalCacheAdapter.Publish(oldAtr with { AtrValue = 777 });
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor).ConfigureAwait(false);
        Require(await database.EventCountAsync(stream).ConfigureAwait(false) == count + 1, "Local WAL-only input was lost before inbox recovery.");
        var recovered = counted.CapturedEvents.Single();
        Require(recovered.CreatedOn == epoch.AddSeconds(2) && recovered.FuturesItiSignal!.IntrinsicTime == walOnly.Timestamp
            && recovered.SourceCursor == walOnly.SourceCursor && recovered.Id == walOnly.CommandId,
            "Recovery replaced timestamps, source identity or deterministic event ID.");
        if (Frame == TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily)
            Require(recovered.FuturesItiSignal!.Threshold == 10, "Recovery substituted today's ATR cache for the frozen input.");

        var root = Path.Combine(Path.GetTempPath(), "ifm-iti-wal-" + Guid.NewGuid().ToString("N"));
        await using (var wal = new FuturesItiInputWal(root))
        {
            _ = await wal.AppendAsync(FuturesItiJournalEnvelope.Encode(SourceCommand(3500)), default).ConfigureAwait(false);
        }
        await using (var wal = new FuturesItiInputWal(root))
        {
            var records = new List<FuturesItiInputWal.Record>();
            await foreach (var record in wal.ReplayAsync().ConfigureAwait(false)) records.Add(record);
            Require(records.Count == 1 && FuturesItiJournalEnvelope.Decode(records[0].Payload, 1).InputsFrozen,
                "WAL failed to preserve an input across recreation.");
        }
        // A corrupt/torn tail is a recovery failure, never fabricated or silently truncated.
        var path = Directory.GetFiles(root, "*.wal").Single();
        await using (var corrupt = new FileStream(path, FileMode.Append, FileAccess.Write)) corrupt.WriteByte(1);
        rejected = false;
        await using (var wal = new FuturesItiInputWal(root))
        {
            try { await foreach (var _ in wal.ReplayAsync().ConfigureAwait(false)) { } }
            catch (EndOfStreamException) { rejected = true; }
        }
        Require(rejected, "Torn WAL tail was not blocked.");
        Console.WriteLine($"FAST ITI verified {Frame}: zero no-change storage/audits; frozen evaluator parity; duplicates; real owner fencing/loss; restart; rollback; gaps/late arrival; context absence; sealed epoch handover; WAL recovery/corruption.");
        await actor.StopAsync().ConfigureAwait(false);
        actor = null!;
    }
}

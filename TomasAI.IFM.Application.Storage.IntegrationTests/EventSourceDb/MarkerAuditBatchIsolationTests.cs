using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed partial class MarkerProjectorPipelineTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Audit_rejection_in_shared_append_window_does_not_reject_unrelated_command(
        bool batched, bool conflicting, bool rejectedFirst)
    {
        var name = "AuditWindow_" + Guid.NewGuid().ToString("N");
        var seed = await Append(batched, name, 1);
        var original = new ProbeCommand { CommandId = seed.Events[0].CommandId, StreamId = seed.Events[0].AggregateId, Value = 1 };
        var codec = new CommandAuditMessagePackCodec();
        var originalHash = codec.Serialize(original).Sha256;
        var fresh = new ProbeCommand { CommandId = Guid.NewGuid(), StreamId = name + ".fresh", Value = 1 };
        var freshId = await fixture.Storage.ActorEventDb.GetEventStreamIdAsync(fresh.StreamId);
        var eventName = await fixture.Storage.ActorEventDb.GetEventNameIdFromDomainEventAsync(seed.Events[0]);
        EventLogAppendRequest Request(ProbeCommand command, long streamId) => new(
            command.StreamId, streamId, command.CommandId,
            [new(eventName, new ProbeEvent { AggregateId = command.StreamId, CommandId = command.CommandId, Value = 1, Projector = name })],
            0, DateTime.UtcNow, CommandAuditEnvelope.Create(command, codec));
        var rejected = Request(conflicting ? original with { Value = 2 } : original, seed.Id);
        var accepted = Request(fresh, freshId);
        long failures = 0, depth = 0, committed = 0;
        using var meter = new MeterListener();
        meter.InstrumentPublished = (instrument, listener) =>
        { if (instrument.Meter.Name == EventLogPersistenceMetrics.MeterName) listener.EnableMeasurementEvents(instrument); };
        meter.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            if (instrument.Name == "ifm.event_log.append.failures") Interlocked.Add(ref failures, value);
            if (instrument.Name == "ifm.event_log.queue.depth") Interlocked.Add(ref depth, value);
            if (instrument.Name == "ifm.event_log.append.commands") Interlocked.Add(ref committed, value);
        });
        meter.Start();
        await using (var writer = new BinaryCopyEventLogAppender(fixture.Provider, true,
            new EventLogPersistenceOptions { WriteMode = EventLogWriteMode.BinaryCopy,
                MaximumEventsPerBatch = 2, MaximumOldestRequestDelay = TimeSpan.FromMilliseconds(500) },
            EventLogSqlLayout.ForBenchmark(fixture.Provider, false, batched)))
        {
            Task<EventLogAppendResult> invalid, valid;
            if (rejectedFirst) { invalid = writer.AppendAsync(rejected).AsTask(); valid = writer.AppendAsync(accepted).AsTask(); }
            else { valid = writer.AppendAsync(accepted).AsTask(); invalid = writer.AppendAsync(rejected).AsTask(); }
            if (conflicting) await Assert.ThrowsAsync<CommandAuditPayloadConflictException>(() => invalid);
            else await Assert.ThrowsAsync<CommandAuditDuplicateException>(() => invalid);
            Assert.Single((await valid.WaitAsync(TimeSpan.FromSeconds(10))).Assignments);
        }
        // One failed shared transaction, then one failed singleton: proves actual batch isolation was exercised.
        Assert.Equal(2, failures);
        Assert.Equal(1, committed);
        Assert.Equal(0, depth);
        Assert.Equal(originalHash, Assert.IsType<byte[]>(await fixture.Sql(
            "SELECT commandpayloadsha256 FROM command_log WHERE commandid=$1", original.CommandId)));
        Assert.Equal(2L, Convert.ToInt64(await fixture.Sql(
            "SELECT count(*) FROM event_projector_state WHERE projectorname=$1", name)));
        Assert.Equal(2L, Convert.ToInt64(await fixture.Sql(
            "SELECT count(*) FROM event_log WHERE commandid=$1 OR commandid=$2", original.CommandId, fresh.CommandId)));
        await Recover(Projector(name));
        await VerifyCompleted(seed, 1);
        await VerifyCompleted(new Stream(freshId, name, []), 1);
    }
}

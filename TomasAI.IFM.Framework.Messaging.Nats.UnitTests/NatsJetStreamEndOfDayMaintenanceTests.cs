using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJetStreamEndOfDayMaintenanceTests
{
    [Fact]
    public async Task Purge_uses_captured_watermarks_and_ignores_abandoned_event_consumers()
    {
        var transport = new FakeMaintenanceTransport(
            [
                "IFM_FuturesRsiSignalEventProjector_REPLAY",
                "IFM_integration_deadbeef_PROCESS",
                "EventStream",
                "IFM_FuturesRsiSignalEventProjector_PROCESS"
            ],
            new Dictionary<string, Queue<JetStreamMaintenanceSnapshot>>
            {
                ["EventStream"] = Snapshots(
                    Snapshot("EventStream", 2_294_807, 2_294_824,
                        Consumer("EventConsumer"),
                        Consumer("abandoned-test-consumer", pending: 1_000))),
                ["IFM_FuturesRsiSignalEventProjector_PROCESS"] = Snapshots(
                    Snapshot("IFM_FuturesRsiSignalEventProjector_PROCESS", 77_710, 77_716,
                        Consumer("FuturesRsiSignalEventProjector-process-worker"))),
                ["IFM_FuturesRsiSignalEventProjector_REPLAY"] = Snapshots(
                    Snapshot("IFM_FuturesRsiSignalEventProjector_REPLAY", 165, 165,
                        Consumer("FuturesRsiSignalEventProjector-replay-worker")))
            });

        var service = Create(transport);
        var result = await service.PurgeCompletedMessagesAsync(CancellationToken.None);

        transport.Purges.Should().Equal(
            ("EventStream", 2_294_825UL),
            ("IFM_FuturesRsiSignalEventProjector_PROCESS", 77_717UL),
            ("IFM_FuturesRsiSignalEventProjector_REPLAY", 166UL));
        transport.Purges.Should().NotContain(value => value.StreamName.Contains("integration"));
        result.PurgedMessages.Should().Be(2_372_682);
    }

    [Fact]
    public async Task Purge_waits_until_a_projector_consumer_has_no_pending_or_inflight_messages()
    {
        var transport = new FakeMaintenanceTransport(
            ["EventStream", "IFM_FundEventProjector_PROCESS"],
            new Dictionary<string, Queue<JetStreamMaintenanceSnapshot>>
            {
                ["EventStream"] = Snapshots(
                    Snapshot("EventStream", 1, 1, Consumer("EventConsumer"))),
                ["IFM_FundEventProjector_PROCESS"] = Snapshots(
                    Snapshot("IFM_FundEventProjector_PROCESS", 5, 5, Consumer("worker", pending: 2)),
                    Snapshot("IFM_FundEventProjector_PROCESS", 5, 5, Consumer("worker", acknowledgementPending: 1)),
                    Snapshot("IFM_FundEventProjector_PROCESS", 5, 5, Consumer("worker")))
            });

        await Create(transport).PurgeCompletedMessagesAsync(CancellationToken.None);

        transport.InspectionCount["IFM_FundEventProjector_PROCESS"].Should().Be(3);
        transport.Purges.Should().Contain(("IFM_FundEventProjector_PROCESS", 6UL));
    }

    [Fact]
    public async Task Purge_fails_without_mutation_when_the_required_event_stream_is_missing()
    {
        var transport = new FakeMaintenanceTransport([], new Dictionary<string, Queue<JetStreamMaintenanceSnapshot>>());

        var action = () => Create(transport).PurgeCompletedMessagesAsync(CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*EventStream*");
        transport.Purges.Should().BeEmpty();
    }

    [Fact]
    public void Options_reject_invalid_drain_configuration()
    {
        var options = new NatsJetStreamEndOfDayMaintenanceOptions { DrainTimeoutSeconds = 0 };

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>().WithMessage("*positive*");
    }

    static NatsJetStreamEndOfDayMaintenance Create(FakeMaintenanceTransport transport)
        => new(
            new NatsJetStreamEndOfDayMaintenanceOptions
            {
                DrainPollMilliseconds = 1,
                DrainTimeoutSeconds = 1
            },
            transport,
            NullLogger<NatsJetStreamEndOfDayMaintenance>.Instance);

    static Queue<JetStreamMaintenanceSnapshot> Snapshots(params JetStreamMaintenanceSnapshot[] values)
        => new(values);

    static JetStreamMaintenanceSnapshot Snapshot(
        string streamName,
        ulong messages,
        ulong lastSequence,
        params JetStreamMaintenanceConsumer[] consumers)
        => new(streamName, messages, lastSequence, consumers);

    static JetStreamMaintenanceConsumer Consumer(
        string name,
        ulong pending = 0,
        ulong acknowledgementPending = 0)
        => new(name, pending, acknowledgementPending);

    sealed class FakeMaintenanceTransport(
        IReadOnlyList<string> streamNames,
        IReadOnlyDictionary<string, Queue<JetStreamMaintenanceSnapshot>> snapshots)
        : INatsJetStreamMaintenanceTransport
    {
        readonly IReadOnlyList<string> _streamNames = streamNames;
        readonly IReadOnlyDictionary<string, Queue<JetStreamMaintenanceSnapshot>> _snapshots = snapshots;
        readonly Dictionary<string, JetStreamMaintenanceSnapshot> _last = new(StringComparer.Ordinal);

        public List<(string StreamName, ulong Sequence)> Purges { get; } = [];
        public Dictionary<string, int> InspectionCount { get; } = new(StringComparer.Ordinal);

        public ValueTask<IReadOnlyList<string>> ListStreamNamesAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(_streamNames);

        public ValueTask<JetStreamMaintenanceSnapshot> GetSnapshotAsync(
            string streamName,
            CancellationToken cancellationToken)
        {
            InspectionCount[streamName] = InspectionCount.GetValueOrDefault(streamName) + 1;
            var queue = _snapshots[streamName];
            if (queue.Count > 0)
                _last[streamName] = queue.Dequeue();
            return ValueTask.FromResult(_last[streamName]);
        }

        public ValueTask<ulong> PurgeBeforeSequenceAsync(
            string streamName,
            ulong sequence,
            CancellationToken cancellationToken)
        {
            Purges.Add((streamName, sequence));
            return ValueTask.FromResult(_last[streamName].Messages);
        }
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJetStreamStartupPurgeTests
{
    [Fact]
    public async Task Purges_only_selected_streams_through_their_captured_sequence()
    {
        var transport = new FakeTransport();
        var purge = new NatsJetStreamStartupPurge(
            transport, NullLogger<NatsJetStreamStartupPurge>.Instance);

        await purge.PurgeAsync(
            name => name == "EventStream" || name.Contains("FuturesTickData", StringComparison.Ordinal),
            CancellationToken.None);

        transport.Purges.Should().Equal(
            ("EventStream", 101UL),
            ("IFM_FuturesTickDataEventProjector_REPLAY", 9UL));
        transport.Inspected.Should().NotContain("IFM_BrokerOrderEventProjector_REPLAY");
    }

    sealed class FakeTransport : INatsJetStreamMaintenanceTransport
    {
        public List<string> Inspected { get; } = [];
        public List<(string, ulong)> Purges { get; } = [];

        public ValueTask<IReadOnlyList<string>> ListStreamNamesAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<string>>([
                "IFM_BrokerOrderEventProjector_REPLAY",
                "IFM_FuturesTickDataEventProjector_REPLAY",
                "EventStream"
            ]);

        public ValueTask<JetStreamMaintenanceSnapshot> GetSnapshotAsync(
            string streamName, CancellationToken cancellationToken)
        {
            Inspected.Add(streamName);
            var (count, last) = streamName == "EventStream" ? (4UL, 100UL) : (3UL, 8UL);
            return ValueTask.FromResult(new JetStreamMaintenanceSnapshot(streamName, count, last, []));
        }

        public ValueTask<ulong> PurgeBeforeSequenceAsync(
            string streamName, ulong sequence, CancellationToken cancellationToken)
        {
            Purges.Add((streamName, sequence));
            return ValueTask.FromResult(1UL);
        }
    }
}

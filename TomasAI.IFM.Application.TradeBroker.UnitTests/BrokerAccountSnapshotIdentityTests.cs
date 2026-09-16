using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class BrokerAccountSnapshotIdentityTests
{
    [Fact]
    public void Exact_snapshot_redelivery_has_the_same_identity_and_a_later_observation_does_not()
    {
        var observedAt = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var first = Evidence(observedAt, [new("ESZ6", 1, 6000m)]);
        var redelivery = Evidence(observedAt, [new("ESZ6", 1, 6000m)]);
        var later = Evidence(observedAt.AddTicks(1), [new("ESZ6", 1, 6000m)]);

        var firstId = BrokerAccountSnapshotIdentity.Create(BrokerEnvironment.Emulator, first);

        Assert.Equal(firstId, BrokerAccountSnapshotIdentity.Create(BrokerEnvironment.Emulator, redelivery));
        Assert.NotEqual(firstId, BrokerAccountSnapshotIdentity.Create(BrokerEnvironment.Emulator, later));
    }

    [Fact]
    public void Snapshot_evidence_canonicalizes_position_order_before_identity_is_created()
    {
        var observedAt = new DateTime(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);
        var forward = Evidence(observedAt,
            [new("ESZ6", 1, 6000m), new("VXU6", -1, 16m)]);
        var reverse = Evidence(observedAt,
            [new("VXU6", -1, 16m), new("ESZ6", 1, 6000m)]);

        Assert.Equal(["ESZ6", "VXU6"],
            forward.Positions.Select(static position => position.ContractId));
        Assert.Equal(
            BrokerAccountSnapshotIdentity.Create(BrokerEnvironment.Emulator, forward),
            BrokerAccountSnapshotIdentity.Create(BrokerEnvironment.Emulator, reverse));
    }

    private static BrokerAccountSnapshotEvidence Evidence(
        DateTime observedAt,
        BrokerAccountPosition[] positions) =>
        BrokerAccountSnapshotEvidence.From(new BrokerAccountSnapshot(
            "IFM-EMULATOR-PAPER", "USD", 100_000m, 90_000m, true, true, 3,
            observedAt, positions));
}

using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests;

public sealed class SpreadDistributionReadModelMappingTests
{
    [Fact]
    public void Trade_snapshot_maps_without_changing_the_persisted_wire_shape()
    {
        var snapshot = new TradeSpreadDistributionSnapshot
        {
            Id = 47,
            TradeId = 73,
            ValueDate = new DateOnly(2026, 9, 13),
            TradeType = TradeType.ShortIronCondor,
            TradeStatus = TradeStatus.Open,
            DaysToExpiry = 19,
            ForwardPrice = 6123.25,
            LossProbability = 0.17,
            LossThreshold = 240.50m,
            LossThresholdCount = 11,
            ShortVolatility = 0.21,
            LongVolatility = 0.18,
            ForwardLossRatio = 0.04,
            CreatedOn = new DateTime(2026, 9, 13, 14, 30, 0, DateTimeKind.Utc)
        };

        var mapped = snapshot.ToSpreadDistributionReadModel();

        mapped.Should().BeEquivalentTo(snapshot);
        MessagePackSerializer.Serialize(mapped)
            .Should().Equal(MessagePackSerializer.Serialize(snapshot));
    }
}

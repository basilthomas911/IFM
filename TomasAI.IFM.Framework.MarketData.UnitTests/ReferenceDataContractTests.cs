using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

public sealed class ReferenceDataContractTests
{
    [Fact]
    public void TreasuryRatePoint_ConvertsPercentagePointsToDecimalRate()
    {
        var point = new TreasuryRatePoint(TreasuryTenor.OneMonth, 4.25m);

        Assert.Equal(0.0425m, point.DecimalRate);
    }

    [Fact]
    public void TreasuryCurveSnapshot_ReturnsRequestedTenor()
    {
        var expected = new TreasuryRatePoint(TreasuryTenor.ThreeMonth, 4.10m);
        var snapshot = new TreasuryCurveSnapshot(
            new DateOnly(2026, 8, 10),
            [
                new TreasuryRatePoint(TreasuryTenor.OneMonth, 4.20m),
                expected
            ],
            new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            "test");

        var found = snapshot.TryGetRate(TreasuryTenor.ThreeMonth, out var actual);

        Assert.True(found);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TreasuryCurveSnapshot_DoesNotTreatMissingTenorAsZero()
    {
        var snapshot = new TreasuryCurveSnapshot(
            new DateOnly(2026, 8, 10),
            [new TreasuryRatePoint(TreasuryTenor.OneMonth, 0m)],
            new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            "test");

        var found = snapshot.TryGetRate(TreasuryTenor.TwoMonth, out _);

        Assert.False(found);
    }
}

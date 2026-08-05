using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

public sealed class EodStatisticsTests
{
    [Fact]
    public void StdDevCalculator_UsesSampleStandardDeviation()
    {
        FuturesEodDataV2ReadModel[] values =
        [
            Create(1m),
            Create(2m),
            Create(3m)
        ];

        var result = new StdDevCalculator(3, values, static value => (double)value.ClosePrice);

        result.Mean.Should().BeApproximately(2.0, 1e-12);
        result.StdDev.Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void StdDevCalculator_BoundsTheInputToTheRequestedWindow()
    {
        FuturesEodDataV2ReadModel[] values =
        [
            Create(2m),
            Create(4m),
            Create(100m)
        ];

        var result = new StdDevCalculator(2, values, static value => (double)value.ClosePrice);

        result.Mean.Should().BeApproximately(3.0, 1e-12);
        result.StdDev.Should().BeApproximately(Math.Sqrt(2.0), 1e-12);
    }

    static FuturesEodDataV2ReadModel Create(decimal closePrice)
        => new() { ContractId = "ESU6", ValueDate = new DateOnly(2026, 8, 5), ClosePrice = closePrice };
}

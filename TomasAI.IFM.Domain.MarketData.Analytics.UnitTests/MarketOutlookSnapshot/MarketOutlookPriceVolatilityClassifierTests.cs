using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookPriceVolatilityClassifierTests
{
    public static TheoryData<decimal?, decimal?> InvalidPrices => new()
    {
        { null, 18m },
        { 18m, null },
        { 0m, 18m },
        { 18m, 0m }
    };

    [Theory]
    [InlineData(18, 19, PriceVolatilityType.Rising)]
    [InlineData(18, 17, PriceVolatilityType.Falling)]
    [InlineData(18, 18, PriceVolatilityType.Flat)]
    public void ValidSessionPrices_AreClassifiedByCurrentVxRelativeToOpen(
        decimal sessionOpen,
        decimal current,
        PriceVolatilityType expected)
    {
        MarketOutlookPriceVolatilityClassifier.Classify(sessionOpen, current)
            .Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(InvalidPrices))]
    public void MissingOrInvalidSessionPrices_AreUnknown(
        decimal? sessionOpen,
        decimal? current)
    {
        MarketOutlookPriceVolatilityClassifier.Classify(sessionOpen, current)
            .Should().Be(PriceVolatilityType.Unknown);
    }
}

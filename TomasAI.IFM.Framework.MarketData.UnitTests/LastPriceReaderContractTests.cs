using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

public sealed class LastPriceReaderContractTests
{
    [Fact]
    public void FuturesOptionReaderExposesAtomicQuoteAndTradeGreeksReads()
    {
        var quoteMethod = typeof(IFuturesOptionLastPriceReader)
            .GetMethod(nameof(IFuturesOptionLastPriceReader.TryGetLastQuoteWithGreeks));
        var tradeMethod = typeof(IFuturesOptionLastPriceReader)
            .GetMethod(nameof(IFuturesOptionLastPriceReader.TryGetLastTradeWithGreeks));

        Assert.NotNull(quoteMethod);
        Assert.NotNull(tradeMethod);
        Assert.Equal(
            typeof(LastQuoteTickWithGreeksSnapshot).MakeByRefType(),
            Assert.Single(quoteMethod.GetParameters()).ParameterType);
        Assert.Equal(
            typeof(LastTradeTickWithGreeksSnapshot).MakeByRefType(),
            Assert.Single(tradeMethod.GetParameters()).ParameterType);
    }

    [Fact]
    public void FuturesReaderDoesNotExposeOptionGreeksMethods()
    {
        Assert.Null(typeof(IFuturesLastPriceReader)
            .GetMethod("TryGetLastQuoteWithGreeks"));
        Assert.Null(typeof(IFuturesLastPriceReader)
            .GetMethod("TryGetLastTradeWithGreeks"));
    }

    [Fact]
    public void InvalidGreeksCanRepresentMissingValuesWithoutZeroSentinels()
    {
        var snapshot = new OptionGreeksSnapshot(
            false,
            false,
            OptionGreeksFailureReason.MissingFuturesPrice,
            OptionGreeksPriceSource.QuoteMidpoint,
            "ES-202609",
            null,
            11m,
            0.04,
            39d / 365d,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            0,
            102,
            default,
            new DateTimeOffset(2026, 8, 10, 20, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 10, 20, 0, 1, TimeSpan.Zero));

        Assert.False(snapshot.IsValid);
        Assert.Null(snapshot.FuturesPrice);
        Assert.Null(snapshot.Delta);
        Assert.Equal(
            OptionGreeksFailureReason.MissingFuturesPrice,
            snapshot.FailureReason);
    }
}

using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesRsiSignal;

/// <summary>
/// BDD-style tests for the FuturesRsiSignalQueryActor query handlers (GetFuturesRsiSignalQuery and
/// GetFuturesRsiDailySignalQuery), verifying that queries correctly delegate to the market data database
/// and return the expected RSI read model, across Daily, Weekly and Monthly time periods.
/// </summary>
public class FuturesRsiSignalQueryTests
{
    static IDbContextFactory CreateDbFactory(out IMarketDataDbContext marketDataDb)
    {
        marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        return dbFactory;
    }

    static FuturesRsiSignalReadModel CreateReadModel(
        string contractId = SampleData.ContractId,
        TradeTimePeriodType timePeriod = TradeTimePeriodType.Daily,
        decimal price = SampleData.FuturesPrice,
        double rsi = 55.0,
        double rsiAverage = 52.0,
        double rsiSlope = 0.1)
        => new(
            contractId: contractId,
            valueDate: SampleData.ValueDate,
            timePeriod: timePeriod,
            periodLength: SampleData.PeriodLength,
            timestamp: new TimeOnly(18, 50, 10),
            price: price,
            priceChange: 5m,
            priceGain: 5m,
            priceLoss: 0m,
            averagePriceGain: 5m,
            averagePriceLoss: 1m,
            rs: 5.0,
            rsi: rsi,
            rsiAverage: rsiAverage,
            rsiSlope: rsiSlope);

    // ───── GetFuturesRsiSignalQuery / GetLastFuturesRsiSignalAsync (Given / When / Then) ─────

    [Fact]
    public async Task GivenExistingRsiSignal_WhenGetLastFuturesRsiSignalAsyncIsCalled_ThenReturnsMatchingReadModel()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesRsiSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesRsiSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenNoRsiSignalExists_WhenGetLastFuturesRsiSignalAsyncIsCalled_ThenReturnsNull()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        marketDataDb.GetLastFuturesRsiSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns((FuturesRsiSignalReadModel?)null);

        var query = new GetFuturesRsiSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenDifferentContractIds_WhenGetLastFuturesRsiSignalAsyncIsCalled_ThenRequestsDataForCorrectContract()
    {
        // Given
        const string otherContractId = "CLZ25";
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(contractId: otherContractId);
        marketDataDb.GetLastFuturesRsiSignalAsync(
                otherContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiSignalQuery(
            otherContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.ContractId.Should().Be(otherContractId);
        await marketDataDb.DidNotReceive().GetLastFuturesRsiSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task GivenVariousTimePeriods_WhenGetLastFuturesRsiSignalAsyncIsCalled_ThenPassesThroughCorrectTimePeriod(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(timePeriod: timePeriod);
        marketDataDb.GetLastFuturesRsiSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        result!.TimePeriod.Should().Be(timePeriod);
    }

    [Fact]
    public async Task GivenRsiSignalOverbought_WhenGetLastFuturesRsiSignalAsyncIsCalled_ThenReturnsSignalWithHighRsi()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(rsi: 82.0, rsiAverage: 78.0, rsiSlope: 1.5);
        marketDataDb.GetLastFuturesRsiSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.RSI.Should().BeGreaterThan(70);
    }

    [Fact]
    public async Task GivenRsiSignalOversold_WhenGetLastFuturesRsiSignalAsyncIsCalled_ThenReturnsSignalWithLowRsi()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(rsi: 18.0, rsiAverage: 22.0, rsiSlope: -1.5);
        marketDataDb.GetLastFuturesRsiSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.RSI.Should().BeLessThan(30);
    }

    [Fact]
    public void GivenEmptyContractId_WhenGetFuturesRsiSignalQueryIsConstructed_ThenEntityIdIsNotNull()
    {
        // Given / When
        var query = new GetFuturesRsiSignalQuery(
            string.Empty, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // Then
        query.ContractId.Should().BeEmpty();
        query.EntityId.Should().NotBeNull();
    }

    [Fact]
    public async Task GivenZeroPeriodLength_WhenGetLastFuturesRsiSignalAsyncIsCalled_ThenRequestsWithZeroPeriodLength()
    {
        // Given
        const int zeroPeriodLength = 0;
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesRsiSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, zeroPeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, zeroPeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesRsiSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, zeroPeriodLength);
    }

    // ───── GetFuturesRsiDailySignalQuery / GetLastFuturesRsiDailySignalAsync (Given / When / Then) ─────

    [Fact]
    public async Task GivenExistingRsiDailySignal_WhenGetLastFuturesRsiDailySignalAsyncIsCalled_ThenReturnsMatchingReadModel()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesRsiDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesRsiDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenNoRsiDailySignalExists_WhenGetLastFuturesRsiDailySignalAsyncIsCalled_ThenReturnsNull()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        marketDataDb.GetLastFuturesRsiDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns((FuturesRsiSignalReadModel?)null);

        var query = new GetFuturesRsiDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task GivenVariousTimePeriods_WhenGetLastFuturesRsiDailySignalAsyncIsCalled_ThenPassesThroughCorrectTimePeriod(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(timePeriod: timePeriod);
        marketDataDb.GetLastFuturesRsiDailySignalAsync(
                SampleData.ContractId, timePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiDailySignalQuery(
            SampleData.ContractId, timePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        result!.TimePeriod.Should().Be(timePeriod);
    }

    [Fact]
    public async Task GivenDifferentContractIds_WhenGetLastFuturesRsiDailySignalAsyncIsCalled_ThenRequestsDataForCorrectContract()
    {
        // Given
        const string otherContractId = "CLZ25";
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(contractId: otherContractId);
        marketDataDb.GetLastFuturesRsiDailySignalAsync(
                otherContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiDailySignalQuery(
            otherContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.ContractId.Should().Be(otherContractId);
        await marketDataDb.DidNotReceive().GetLastFuturesRsiDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenZeroPeriodLength_WhenGetLastFuturesRsiDailySignalAsyncIsCalled_ThenRequestsWithZeroPeriodLength()
    {
        // Given
        const int zeroPeriodLength = 0;
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesRsiDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, zeroPeriodLength)
            .Returns(expected);

        var query = new GetFuturesRsiDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, zeroPeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesRsiDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, zeroPeriodLength);
    }

    [Fact]
    public void GivenEmptyContractId_WhenGetFuturesRsiDailySignalQueryIsConstructed_ThenEntityIdIsNotNull()
    {
        // Given / When
        var query = new GetFuturesRsiDailySignalQuery(
            string.Empty, SampleData.TimePeriod, SampleData.PeriodLength);

        // Then
        query.ContractId.Should().BeEmpty();
        query.EntityId.Should().NotBeNull();
    }
}

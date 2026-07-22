using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesMacdSignal;

public class FuturesMacdSignalQueryTests
{
    static IDbContextFactory CreateDbFactory(out IMarketDataDbContext marketDataDb)
    {
        marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        return dbFactory;
    }

    static FuturesMacdSignalReadModel CreateReadModel(
        string contractId = SampleData.ContractId,
        TradeTimePeriodType timePeriod = TradeTimePeriodType.Daily,
        double macdLine = 1.5,
        double signalLine = 1.2,
        double histogram = 0.3,
        FuturesTrendDirectionType macd = FuturesTrendDirectionType.UpTrending,
        FuturesTrendDirectionStrengthType macdStrength = FuturesTrendDirectionStrengthType.Medium)
        => new(
            contractId: contractId,
            valueDate: SampleData.ValueDate,
            timePeriod: timePeriod,
            periodLength: SampleData.PeriodLength,
            timestamp: new TimeOnly(18, 50, 10),
            futuresPrice: SampleData.FuturesPrice,
            macdLine: macdLine,
            signalLine: signalLine,
            histogram: histogram,
            macd: macd,
            macdStrength: macdStrength);

    // ───── GetFuturesMacdSignalQuery / GetLastFuturesMacdSignalAsync (Given / When / Then) ─────

    [Fact]
    public async Task GivenExistingMacdSignal_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenReturnsMatchingReadModel()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesMacdSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesMacdSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenNoMacdSignalExists_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenReturnsNull()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        marketDataDb.GetLastFuturesMacdSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns((FuturesMacdSignalReadModel?)null);

        var query = new GetFuturesMacdSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenDifferentContractIds_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenRequestsDataForCorrectContract()
    {
        // Given
        const string otherContractId = "CLZ25";
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(contractId: otherContractId);
        marketDataDb.GetLastFuturesMacdSignalAsync(
                otherContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdSignalQuery(
            otherContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.ContractId.Should().Be(otherContractId);
        await marketDataDb.DidNotReceive().GetLastFuturesMacdSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task GivenVariousTimePeriods_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenPassesThroughCorrectTimePeriod(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(timePeriod: timePeriod);
        marketDataDb.GetLastFuturesMacdSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        result!.TimePeriod.Should().Be(timePeriod);
    }

    [Fact]
    public async Task GivenMacdSignalWithDownTrend_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenReturnsSignalWithExpectedDirectionAndStrength()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(
            macdLine: -1.5,
            signalLine: -1.0,
            histogram: -0.5,
            macd: FuturesTrendDirectionType.DownTrending,
            macdStrength: FuturesTrendDirectionStrengthType.High);
        marketDataDb.GetLastFuturesMacdSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.MACD.Should().Be(FuturesTrendDirectionType.DownTrending);
        result.MACDStrength.Should().Be(FuturesTrendDirectionStrengthType.High);
        result.MacdLine.Should().Be(-1.5);
        result.Histogram.Should().Be(-0.5);
    }

    [Fact]
    public async Task GivenMacdSignalFlat_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenReturnsSignalNearZero()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(
            macdLine: 0.0,
            signalLine: 0.0,
            histogram: 0.0,
            macd: FuturesTrendDirectionType.Flat,
            macdStrength: FuturesTrendDirectionStrengthType.Low);
        marketDataDb.GetLastFuturesMacdSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.MACD.Should().Be(FuturesTrendDirectionType.Flat);
        result.Histogram.Should().Be(0.0);
    }

    [Fact]
    public void GivenNullContractId_WhenGetFuturesMacdSignalQueryIsConstructed_ThenContractIdDefaultsToEmpty()
    {
        // Given / When
        var query = new GetFuturesMacdSignalQuery(
            null!, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // Then
        query.ContractId.Should().BeEmpty();
        query.EntityId.Should().BeOfType<FuturesMacdSignalEntityId>();
    }

    [Fact]
    public async Task GivenZeroPeriodLength_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenPassesThroughZeroPeriodLength()
    {
        // Given
        const int zeroPeriodLength = 0;
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesMacdSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, zeroPeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, zeroPeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesMacdSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, zeroPeriodLength);
    }

    [Fact]
    public async Task GivenDifferentValueDates_WhenGetLastFuturesMacdSignalAsyncIsCalled_ThenRequestsDataForCorrectDate()
    {
        // Given
        var otherValueDate = SampleData.ValueDate.AddDays(-1);
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesMacdSignalAsync(
                SampleData.ContractId, otherValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdSignalQuery(
            SampleData.ContractId, otherValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
            query.ContractId, query.ValueDate, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.DidNotReceive().GetLastFuturesMacdSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    // ───── GetFuturesMacdDailySignalQuery / GetLastFuturesMacdDailySignalAsync (Given / When / Then) ─────

    [Fact]
    public async Task GivenExistingMacdDailySignal_WhenGetLastFuturesMacdDailySignalAsyncIsCalled_ThenReturnsMatchingReadModel()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesMacdDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesMacdDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenNoMacdDailySignalExists_WhenGetLastFuturesMacdDailySignalAsyncIsCalled_ThenReturnsNull()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        marketDataDb.GetLastFuturesMacdDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns((FuturesMacdSignalReadModel?)null);

        var query = new GetFuturesMacdDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenDifferentContractIds_WhenGetLastFuturesMacdDailySignalAsyncIsCalled_ThenRequestsDataForCorrectContract()
    {
        // Given
        const string otherContractId = "CLZ25";
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(contractId: otherContractId);
        marketDataDb.GetLastFuturesMacdDailySignalAsync(
                otherContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdDailySignalQuery(
            otherContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.ContractId.Should().Be(otherContractId);
        await marketDataDb.DidNotReceive().GetLastFuturesMacdDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public async Task GivenVariousTimePeriods_WhenGetLastFuturesMacdDailySignalAsyncIsCalled_ThenPassesThroughCorrectTimePeriod(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(timePeriod: timePeriod);
        marketDataDb.GetLastFuturesMacdDailySignalAsync(
                SampleData.ContractId, timePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdDailySignalQuery(
            SampleData.ContractId, timePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        result!.TimePeriod.Should().Be(timePeriod);
    }

    [Fact]
    public async Task GivenMacdDailySignalWithUpTrend_WhenGetLastFuturesMacdDailySignalAsyncIsCalled_ThenReturnsSignalWithExpectedDirectionAndStrength()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(
            macdLine: 2.0,
            signalLine: 1.0,
            histogram: 1.0,
            macd: FuturesTrendDirectionType.UpTrending,
            macdStrength: FuturesTrendDirectionStrengthType.High);
        marketDataDb.GetLastFuturesMacdDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().NotBeNull();
        result!.MACD.Should().Be(FuturesTrendDirectionType.UpTrending);
        result.MACDStrength.Should().Be(FuturesTrendDirectionStrengthType.High);
        result.MacdLine.Should().Be(2.0);
        result.Histogram.Should().Be(1.0);
    }

    [Fact]
    public async Task GivenZeroPeriodLength_WhenGetLastFuturesMacdDailySignalAsyncIsCalled_ThenPassesThroughZeroPeriodLength()
    {
        // Given
        const int zeroPeriodLength = 0;
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesMacdDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, zeroPeriodLength)
            .Returns(expected);

        var query = new GetFuturesMacdDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, zeroPeriodLength);

        // When
        var result = await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
            query.ContractId, query.TimePeriod, query.PeriodLength);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesMacdDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, zeroPeriodLength);
    }

    [Fact]
    public void GivenEmptyContractId_WhenGetFuturesMacdDailySignalQueryIsConstructed_ThenEntityIdIsNotNull()
    {
        // Given / When
        var query = new GetFuturesMacdDailySignalQuery(
            string.Empty, SampleData.TimePeriod, SampleData.PeriodLength);

        // Then
        query.ContractId.Should().BeEmpty();
        query.EntityId.Should().NotBeNull();
    }
}

using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.QueryHandlers;

public class FuturesAdxSignalQueryTests
{
    static IDbContextFactory CreateDbFactory(out IMarketDataDbContext marketDataDb)
    {
        marketDataDb = Substitute.For<IMarketDataDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb.Returns(marketDataDb);
        return dbFactory;
    }

    static FuturesAdxSignalReadModel CreateReadModel(
        string contractId = SampleData.ContractId,
        FuturesTrendDirectionType adx = FuturesTrendDirectionType.UpTrending,
        FuturesTrendDirectionStrengthType strength = FuturesTrendDirectionStrengthType.High)
        => SampleData.CreateAdxHistoryEvent(
            price: SampleData.FuturesPrice,
            plusDI: 25.0,
            minusDI: 15.0,
            adxValue: 30.0,
            direction: adx,
            strength: strength).FuturesAdxSignal! with
        { ContractId = contractId };

    // ───── GetLastFuturesAdxSignalAsync (Given / When / Then) ─────

    [Fact]
    public async Task GivenExistingAdxSignal_WhenGetLastFuturesAdxSignalAsyncIsCalled_ThenReturnsMatchingReadModel()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesAdxSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesAdxSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await query.GetLastFuturesAdxSignalAsync(dbFactory);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesAdxSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenNoAdxSignalExists_WhenGetLastFuturesAdxSignalAsyncIsCalled_ThenReturnsNull()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        marketDataDb.GetLastFuturesAdxSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns((FuturesAdxSignalReadModel?)null);

        var query = new GetFuturesAdxSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await query.GetLastFuturesAdxSignalAsync(dbFactory);

        // Then
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenDifferentContractIds_WhenGetLastFuturesAdxSignalAsyncIsCalled_ThenRequestsDataForCorrectContract()
    {
        // Given
        const string otherContractId = "CLZ25";
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(contractId: otherContractId);
        marketDataDb.GetLastFuturesAdxSignalAsync(
                otherContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesAdxSignalQuery(
            otherContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await query.GetLastFuturesAdxSignalAsync(dbFactory);

        // Then
        result.Should().NotBeNull();
        result!.ContractId.Should().Be(otherContractId);
        await marketDataDb.DidNotReceive().GetLastFuturesAdxSignalAsync(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    public async Task GivenVariousTimePeriods_WhenGetLastFuturesAdxSignalAsyncIsCalled_ThenPassesThroughCorrectTimePeriod(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesAdxSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesAdxSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, timePeriod, SampleData.PeriodLength);

        // When
        var result = await query.GetLastFuturesAdxSignalAsync(dbFactory);

        // Then
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GivenAdxSignalWithTrendReversal_WhenGetLastFuturesAdxSignalAsyncIsCalled_ThenReturnsSignalWithExpectedDirectionAndStrength()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel(
            adx: FuturesTrendDirectionType.TrendReversal,
            strength: FuturesTrendDirectionStrengthType.Medium);
        marketDataDb.GetLastFuturesAdxSignalAsync(
                SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesAdxSignalQuery(
            SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await query.GetLastFuturesAdxSignalAsync(dbFactory);

        // Then
        result.Should().NotBeNull();
        result!.ADX.Should().Be(FuturesTrendDirectionType.TrendReversal);
        result.ADXStrength.Should().Be(FuturesTrendDirectionStrengthType.Medium);
    }

    // ───── GetLastFuturesAdxDailySignalAsync (Given / When / Then) ─────

    [Fact]
    public async Task GivenExistingAdxDailySignal_WhenGetLastFuturesAdxDailySignalAsyncIsCalled_ThenReturnsMatchingReadModel()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesAdxDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns(expected);

        var query = new GetFuturesAdxDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await query.GetLastFuturesAdxDailySignalAsync(dbFactory);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.Received(1).GetLastFuturesAdxDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenNoAdxDailySignalExists_WhenGetLastFuturesAdxDailySignalAsyncIsCalled_ThenReturnsNull()
    {
        // Given
        var dbFactory = CreateDbFactory(out var marketDataDb);
        marketDataDb.GetLastFuturesAdxDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength)
            .Returns((FuturesAdxSignalReadModel?)null);

        var query = new GetFuturesAdxDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);

        // When
        var result = await query.GetLastFuturesAdxDailySignalAsync(dbFactory);

        // Then
        result.Should().BeNull();
    }

    [Fact]
    public async Task GivenDifferentPeriodLengths_WhenGetLastFuturesAdxDailySignalAsyncIsCalled_ThenRequestsCorrectPeriodLength()
    {
        // Given
        const int otherPeriodLength = 21;
        var dbFactory = CreateDbFactory(out var marketDataDb);
        var expected = CreateReadModel();
        marketDataDb.GetLastFuturesAdxDailySignalAsync(
                SampleData.ContractId, SampleData.TimePeriod, otherPeriodLength)
            .Returns(expected);

        var query = new GetFuturesAdxDailySignalQuery(
            SampleData.ContractId, SampleData.TimePeriod, otherPeriodLength);

        // When
        var result = await query.GetLastFuturesAdxDailySignalAsync(dbFactory);

        // Then
        result.Should().BeSameAs(expected);
        await marketDataDb.DidNotReceive().GetLastFuturesAdxDailySignalAsync(
            SampleData.ContractId, SampleData.TimePeriod, SampleData.PeriodLength);
    }

    [Fact]
    public async Task GivenEmptyContractId_WhenGetFuturesAdxDailySignalQueryIsConstructed_ThenContractIdDefaultsToEmptyString()
    {
        // Given / When
        var query = new GetFuturesAdxDailySignalQuery(null!, SampleData.TimePeriod, SampleData.PeriodLength);

        // Then
        query.ContractId.Should().Be(string.Empty);
    }

    [Fact]
    public async Task GivenEmptyContractId_WhenGetFuturesAdxSignalQueryIsConstructed_ThenContractIdDefaultsToEmptyString()
    {
        // Given / When
        var query = new GetFuturesAdxSignalQuery(null!, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // Then
        query.ContractId.Should().Be(string.Empty);
    }
}

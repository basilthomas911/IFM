using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Query;

/// <summary>Verifies the typed results returned by Query handlers.</summary>
public sealed class AlignedAnalyticsQueryHandlerTests
{
    /// <summary>A missing historical attempt produces a successful, nullable typed reply.</summary>
    [Fact]
    public async Task HistoricalAttemptMissing_ReturnsNullDiagnostics()
    {
        var id = Guid.NewGuid();
        var query = new GetFuturesAnalyticsHistoricalDataLoaderQuery(id) with
        {
            Subject = new(ActorType.Query, GetFuturesAnalyticsHistoricalDataLoaderQuery.Actor,
                GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb, id.ToString("D"))
        };
        var context = Substitute.For<IFuturesAnalyticsHistoricalDataLoaderQueryContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        context.DbFactory.Returns(dbFactory);
        dbFactory.MarketDataDb.Returns(Substitute.For<IMarketDataDbContext>());

        var result = await query.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    /// <summary>Cancelling a diagnostics read prevents the Query reply.</summary>
    [Fact]
    public async Task HistoricalAttemptCancelled_DoesNotReply()
    {
        var id = Guid.NewGuid();
        var query = new GetFuturesAnalyticsHistoricalDataLoaderQuery(id) with
        {
            Subject = new(ActorType.Query, GetFuturesAnalyticsHistoricalDataLoaderQuery.Actor,
                GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb, id.ToString("D"))
        };
        var context = Substitute.For<IFuturesAnalyticsHistoricalDataLoaderQueryContext>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await query.ExecuteAsync(context, cancellation.Token));

        await context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default, default!, default(ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>)!);
    }

    /// <summary>The VX latest handler keeps a null database result in the typed reply.</summary>
    [Fact]
    public async Task VxLatestMissing_ReturnsNullObservation()
    {
        var query = new GetLatestFuturesVxTermStructureSignalQuery
        {
            ValueDate = new(2026, 9, 15),
            ConfigurationId = "test",
            Subject = new(ActorType.Query, GetLatestFuturesVxTermStructureSignalQuery.Actor,
                GetLatestFuturesVxTermStructureSignalQuery.Verb, Guid.NewGuid().ToString("D"))
        };
        var context = Substitute.For<IFuturesVxTermStructureSignalQueryContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketData = Substitute.For<IMarketDataDbContext>();
        context.DbFactory.Returns(dbFactory);
        dbFactory.MarketDataDb.Returns(marketData);
        marketData.GetLatestFuturesVxTermStructureSignalAsync(
                query.ValueDate, query.ConfigurationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesVxTermStructureSignalReadModel?>(null));

        var result = await query.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    /// <summary>The VWAP latest handler keeps a null database result in the typed reply.</summary>
    [Fact]
    public async Task VwapLatestMissing_ReturnsNullSignal()
    {
        var query = new GetLatestFuturesVwapSignalQuery
        {
            ContractId = "ES",
            ValueDate = new(2026, 9, 15),
            ConfigurationId = "test",
            Subject = new(ActorType.Query, GetLatestFuturesVwapSignalQuery.Actor,
                GetLatestFuturesVwapSignalQuery.Verb, Guid.NewGuid().ToString("D"))
        };
        var context = Substitute.For<IFuturesVwapSignalQueryContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var marketData = Substitute.For<IMarketDataDbContext>();
        context.DbFactory.Returns(dbFactory);
        dbFactory.MarketDataDb.Returns(marketData);
        marketData.GetLatestFuturesVwapSignalAsync(query.ContractId,
                query.ValueDate, query.ConfigurationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesVwapSignalReadModel?>(null));

        var result = await query.ExecuteAsync(context, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }
}

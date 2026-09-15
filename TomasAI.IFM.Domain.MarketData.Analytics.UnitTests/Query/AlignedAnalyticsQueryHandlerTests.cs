using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
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

/// <summary>Verifies the typed replies of Query handlers moved out of actor receive maps.</summary>
public sealed class AlignedAnalyticsQueryHandlerTests
{
    /// <summary>A missing historical attempt produces a successful, nullable typed reply.</summary>
    [Fact]
    public async Task HistoricalAttemptMissing_RepliesWithNullDiagnostics()
    {
        var id = Guid.NewGuid();
        var query = new GetFuturesAnalyticsHistoricalDataLoaderQuery(id) with
        {
            Subject = new(ActorType.Query, GetFuturesAnalyticsHistoricalDataLoaderQuery.Actor,
                GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb, id.ToString("D"))
        };
        var context = Substitute.For<IFuturesAnalyticsHistoricalDataLoaderQueryContext>();
        var store = Substitute.For<IHistoricalDataLoaderStore>();
        context.DataLoaderStore.Returns(store);
        store.GetAsync(id, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<HistoricalDataLoaderState?>((HistoricalDataLoaderState?)null));

        await query.ExecuteAsync(context, CancellationToken.None);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId,
            GetFuturesAnalyticsHistoricalDataLoaderQuery.Verb,
            Arg.Is<ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>>(
                result => result.Success && result.Value == null));
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
        var store = Substitute.For<IHistoricalDataLoaderStore>();
        context.DataLoaderStore.Returns(store);
        store.GetAsync(id, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<HistoricalDataLoaderState?>((HistoricalDataLoaderState?)null));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await query.ExecuteAsync(context, cancellation.Token));

        await context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default, default!, default(ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>)!);
    }

    /// <summary>The VX latest handler keeps a null database result in the typed reply.</summary>
    [Fact]
    public async Task VxLatestMissing_RepliesWithNullObservation()
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

        await query.ExecuteAsync(context, CancellationToken.None);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId,
            GetLatestFuturesVxTermStructureSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesVxTermStructureSignalReadModel?>>(
                result => result.Success && result.Value == null));
    }

    /// <summary>The VWAP latest handler keeps a null database result in the typed reply.</summary>
    [Fact]
    public async Task VwapLatestMissing_RepliesWithNullSignal()
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

        await query.ExecuteAsync(context, context, CancellationToken.None);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId,
            GetLatestFuturesVwapSignalQuery.Verb,
            Arg.Is<ServiceResult<FuturesVwapSignalReadModel?>>(
                result => result.Success && result.Value == null));
    }
}

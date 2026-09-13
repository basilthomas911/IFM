using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.QueryParameters;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Event.Extensions;

/// <summary>
/// Provides extension methods on <see cref="IEventActorContext"/> for querying trade and market data,
/// and for issuing commands that drive the spread distribution job lifecycle.
/// </summary>
internal static class SpreadDistributionJobEventExtensions
{
    /// <summary>
    /// Retrieves the option trade read model for the specified order and trade identifiers.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the query.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>The <see cref="OptionTradeReadModel"/> when the query succeeds; otherwise <see langword="null"/>.</returns>
    internal static async ValueTask<OptionTradeReadModel> GetOptionTradeAsync(this IEventActorContext context,int orderId, int tradeId)
    {
        var optionTrade = default(OptionTradeReadModel);
        var entityId = new GetOptionTradeParameter(orderId, tradeId);
        GetOptionTradeQuery query = new(orderId, tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionTradeQuery.Actor, GetOptionTradeQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetOptionTradeQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<OptionTradeReadModel, GetOptionTradeQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            optionTrade = serviceResult.Value;
        return optionTrade!;
    }

    /// <summary>
    /// Retrieves the iron condor market data for the given contract identifiers and date range.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the query.</param>
    /// <param name="underlyingContractId">The underlying futures contract identifier.</param>
    /// <param name="shortPutOptionContractId">The short put option contract identifier.</param>
    /// <param name="longPutOptionContractId">The long put option contract identifier.</param>
    /// <param name="shortCallOptionContractId">The short call option contract identifier.</param>
    /// <param name="longCallOptionContractId">The long call option contract identifier.</param>
    /// <param name="startDate">The start date of the data range.</param>
    /// <param name="endDate">The end date of the data range.</param>
    /// <param name="marketType">The market type.</param>
    /// <param name="currencyType">The currency type.</param>
    /// <returns>The <see cref="IronCondorMarketDataReadModel"/> when the query succeeds; otherwise <see langword="null"/>.</returns>
    internal static async ValueTask<IronCondorMarketDataReadModel> GetIronCondorMarketDataAsync(
        this IEventActorContext context,
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType)
    {
        var ironCondorMarketData = default(IronCondorMarketDataReadModel);
        var entityId = new GetIronCondorMarketDataParameter(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            startDate,
            endDate,
            marketType,
            currencyType);
        GetIronCondorMarketDataQuery query = new(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            startDate,
            endDate,
            marketType,
            currencyType)
        {
            Subject = new ActorSubject(ActorType.Query, GetIronCondorMarketDataQuery.Actor, GetIronCondorMarketDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetIronCondorMarketDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<IronCondorMarketDataReadModel, GetIronCondorMarketDataQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            ironCondorMarketData = serviceResult.Value;
        return ironCondorMarketData!;
    }

    /// <summary>
    /// Retrieves the iron condor market data feed for the given contract identifiers and value date.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the query.</param>
    /// <param name="underlyingContractId">The underlying futures contract identifier.</param>
    /// <param name="shortPutOptionContractId">The short put option contract identifier.</param>
    /// <param name="longPutOptionContractId">The long put option contract identifier.</param>
    /// <param name="shortCallOptionContractId">The short call option contract identifier.</param>
    /// <param name="longCallOptionContractId">The long call option contract identifier.</param>
    /// <param name="valueDate">The value date for which to retrieve the market data feed.</param>
    /// <returns>The <see cref="IronCondorMarketDataFeedReadModel"/> when the query succeeds; otherwise <see langword="null"/>.</returns>
    internal static async ValueTask<IronCondorMarketDataFeedReadModel> GetIronCondorMarketDataFeedAsync(
        this IEventActorContext context,
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly valueDate)
    {
        var ironCondorMarketDataFeed = default(IronCondorMarketDataFeedReadModel);
        var entityId = new GetIronCondorMarketDataFeedParameter(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            valueDate);
        GetIronCondorMarketDataFeedQuery query = new(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetIronCondorMarketDataFeedQuery.Actor, GetIronCondorMarketDataFeedQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetIronCondorMarketDataFeedQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<IronCondorMarketDataFeedReadModel, GetIronCondorMarketDataFeedQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            ironCondorMarketDataFeed = serviceResult.Value;
        return ironCondorMarketDataFeed!;
    }

    /// <summary>
    /// Issues a command to mark a spread distribution job as completed.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="entityId">The entity identifier of the spread distribution job.</param>
    /// <param name="jobCompleted">The date and time the job completed.</param>
    /// <param name="jobStatus">The resulting job status.</param>
    /// <exception cref="InvalidOperationException">Thrown when the command does not succeed.</exception>
    internal static async ValueTask CompleteSpreadDistributionJobAsync(
        this IEventActorContext commandApi,
        SpreadDistributionJobEntityId entityId,
        DateTime jobCompleted,
        SpreadDistributionJobStatus jobStatus)
    {
        _ = await OptionPricerCommandApiExtensions.CompleteSpreadDistributionJobAsync(commandApi, entityId, jobCompleted, jobStatus);
    }

    /// <summary>
    /// Issues a command to mark a spread distribution job as failed.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="entityId">The entity identifier of the spread distribution job.</param>
    /// <param name="jobFailed">The date and time the job failed.</param>
    /// <param name="jobStatus">The resulting job status.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <exception cref="InvalidOperationException">Thrown when the command does not succeed.</exception>
    internal static async ValueTask FailSpreadDistributionJobAsync(
        this IEventActorContext commandApi,
        SpreadDistributionJobEntityId entityId,
        DateTime jobFailed,
        SpreadDistributionJobStatus jobStatus,
        string errorMessage)
    {
        _ = await OptionPricerCommandApiExtensions.FailSpreadDistributionJobAsync(commandApi, entityId, jobFailed, jobStatus, errorMessage);
    }
}

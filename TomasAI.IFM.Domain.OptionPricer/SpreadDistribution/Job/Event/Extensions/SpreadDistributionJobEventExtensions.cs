using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.Trade.ViewModels;

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
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesEodDataQuery.Actor, GetLastFuturesEodDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesEodDataQuery.ErrorId
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
    /// Asynchronously updates the spread distribution statistics for a specified option trade using the provided
    /// distribution models and trade details.
    /// </summary>
    /// <param name="context">The event actor context used to execute the update command and handle the request asynchronously.</param>
    /// <param name="orderId">The unique identifier of the order associated with the option trade.</param>
    /// <param name="tradeId">The unique identifier of the option trade to update.</param>
    /// <param name="tradeType">The type of the option trade, indicating whether it is a put or call.</param>
    /// <param name="valueDate">The value date for which the spread distribution statistics are being updated.</param>
    /// <param name="tradeStatus">The current status of the option trade, which may affect the update process.</param>
    /// <param name="putSpreadDistribution">The spread distribution read model containing statistical data for put options.</param>
    /// <param name="callSpreadDistribution">The spread distribution read model containing statistical data for call options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the update operation fails or the service result indicates an error.</exception>
    internal static async ValueTask UpdateSpreadDistributionStatisticsAsync(
        this IEventActorContext context,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
    {
        var entityId = new OptionTradeEntityId(orderId, tradeId);
        UpdateOptionTradeSpreadDistributionStatisticsCommand cmd = new(
            orderId,
            tradeId,
            tradeType,
            tradeStatus,
            valueDate,
            putSpreadDistribution.DaysToExpiry,
            putSpreadDistribution,
            callSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, UpdateOptionTradeSpreadDistributionStatisticsCommand.Actor, UpdateOptionTradeSpreadDistributionStatisticsCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serviceResult = await context.RequestAsync<UpdateOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    /// <summary>
    /// Asynchronously changes the spread distribution statistics for a specified option trade using the provided
    /// distribution models and trade details.
    /// </summary>
    /// <param name="context">The event actor context used to execute the change command and handle the request asynchronously.</param>
    /// <param name="orderId">The unique identifier of the order associated with the option trade.</param>
    /// <param name="tradeId">The unique identifier of the option trade to change.</param>
    /// <param name="tradeType">The type of the option trade, indicating whether it is a put or call.</param>
    /// <param name="valueDate">The value date for which the spread distribution statistics are being changed.</param>
    /// <param name="tradeStatus">The current status of the option trade, which may affect the change process.</param>
    /// <param name="putSpreadDistribution">The spread distribution read model containing statistical data for put options.</param>
    /// <param name="callSpreadDistribution">The spread distribution read model containing statistical data for call options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the change operation fails or the service result indicates an error.</exception>
    internal static async ValueTask ChangeSpreadDistributionStatisticsAsync(
        this IEventActorContext context,
        int orderId,
        int tradeId,
        double forwardLossRatio,
        double lossProbability,
        DateOnly valueDate)
    {
        var entityId = new OptionTradeEntityId(orderId, tradeId);
        ChangeOptionTradeSpreadDistributionStatisticsCommand cmd = new(
            orderId,
            tradeId,
            forwardLossRatio,
            lossProbability,
            valueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ChangeOptionTradeSpreadDistributionStatisticsCommand.Actor, ChangeOptionTradeSpreadDistributionStatisticsCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serviceResult = await context.RequestAsync<ChangeOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
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
        this IEventActorContext context,
        SpreadDistributionJobEntityId entityId,
        DateTime jobCompleted,
        SpreadDistributionJobStatus jobStatus)
    {
        CompleteSpreadDistributionJobCommand cmd = new(entityId, jobCompleted, jobStatus)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, CompleteSpreadDistributionJobCommand.Actor, CompleteSpreadDistributionJobCommand.Verb, entityId.Format()),
        };
        var serviceResult = await context.RequestAsync<CompleteSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
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
        this IEventActorContext context,
        SpreadDistributionJobEntityId entityId,
        DateTime jobFailed,
        SpreadDistributionJobStatus jobStatus,
        string errorMessage)
    {
        FailSpreadDistributionJobCommand cmd = new(entityId, jobFailed, jobStatus, errorMessage)
        {
            Subject = new ActorSubject(ActorType.Command, FailSpreadDistributionJobCommand.Actor, FailSpreadDistributionJobCommand.Verb, entityId.Format()),
        };
        var serviceResult = await context.RequestAsync<FailSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }
}

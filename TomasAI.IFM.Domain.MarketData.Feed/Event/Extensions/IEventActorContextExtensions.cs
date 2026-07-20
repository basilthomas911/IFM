using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;

public static class IEventActorContextExtensions
{
    internal static async ValueTask StopFuturesBarDataStreamingAsync(this IEventActorContext context, DateOnly valueDate)
    {
        var entityId = new FuturesBarDataStreamingId(valueDate);
        StopFuturesBarDataStreamingCommand cmd = new(valueDate)
        {
            Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = StopFuturesBarDataStreamingCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<StopFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    internal static async ValueTask<YieldCurveRateReadModel> GetLastYieldCurveRateAsync(this IEventActorContext context)
    {
        var entityId = new GetLastYieldCurveRateParameter();
        GetLastYieldCurveRateQuery query = new(true)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastYieldCurveRateQuery.Actor, GetLastYieldCurveRateQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastYieldCurveRateQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<YieldCurveRateReadModel, GetLastYieldCurveRateQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }

    internal static async ValueTask<FuturesContractV2ReadModel> GetCurrentlyTradedFuturesContractAsync(this IEventActorContext context, string symbol)
    {
        var entityId = new GetCurrentlyTradedFuturesContractParameter(symbol);
        GetCurrentlyTradedFuturesContractQuery query = new(symbol)
        {
            Subject = new ActorSubject(ActorType.Query, GetCurrentlyTradedFuturesContractQuery.Actor, GetCurrentlyTradedFuturesContractQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetCurrentlyTradedFuturesContractQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<FuturesContractV2ReadModel, GetCurrentlyTradedFuturesContractQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new()!;
    }

    internal static async ValueTask TurnTradeLiveFeedOffAsync(this IEventActorContext context, Guid commandId, int orderId, int tradeId, DateOnly valueDate)
    {
        var entityId = new TradeLiveFeedId(orderId, tradeId, valueDate);
        TurnTradeLiveFeedOffCommand cmd = new(orderId, tradeId, valueDate)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, TurnTradeLiveFeedOffCommand.Actor, TurnTradeLiveFeedOffCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = TurnTradeLiveFeedOffCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<TurnTradeLiveFeedOffCommand, TradeLiveFeedId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    internal static async ValueTask StopFuturesOptionTickDataStreamingAsync(this IEventActorContext context, Guid commandId, FuturesOptionTickEntityId entityId, string contractId)
    {
        StopFuturesOptionTickDataStreamingCommand cmd = new(entityId, contractId)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = StopFuturesOptionTickDataStreamingCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<StopFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    internal static async ValueTask MarketDataFeedResetCompleteAsync(this IEventActorContext context, MarketDataFeedResetEvent e)
    {
        var completeEvent = e.ToCompleteEvent<MarketDataFeedResetCompleteEvent, MarketDataFeedId>() as MarketDataFeedResetCompleteEvent;
        await context.SendAsync<MarketDataFeedResetCompleteEvent, MarketDataFeedId>(completeEvent!);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <param name="ex"></param>
    /// <returns></returns>
    internal static async ValueTask MarketDataFeedResetFailAsync(this IEventActorContext context, MarketDataFeedResetEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<MarketDataFeedResetFailEvent, MarketDataFeedId>(ex) as MarketDataFeedResetFailEvent;
        await context.SendAsync<MarketDataFeedResetFailEvent, MarketDataFeedId>(failEvent!);
    }

    internal static async ValueTask SendTradeLiveFeedRemovedFailEventAsync(this IEventActorContext context, TradeLiveFeedRemovedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<TradeLiveFeedRemovedFailEvent, TradeLiveFeedId>(ex) as TradeLiveFeedRemovedFailEvent;
        await context.SendAsync<TradeLiveFeedRemovedFailEvent, TradeLiveFeedId>(failEvent!);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <param name="futuresContract"></param>
    /// <param name="entityId"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask StartFuturesTickDataStreamingAsync(this IEventActorContext context, IEvent e, FuturesContractV2ReadModel futuresContract, FuturesDataId entityId)
    {
        StartFuturesTickDataStreamingCommand cmd = e switch
        {
            MarketDataFeedStartedCompleteEvent o => new(futuresContract, o.ValueDate, o.ResetStream)
            {
                Subject = new ActorSubject(ActorType.Command, StartFuturesTickDataStreamingCommand.Actor, StartFuturesTickDataStreamingCommand.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = StartFuturesTickDataStreamingCommand.ErrorId
            },
            MarketDataFeedResetCompleteEvent o => new(futuresContract, o.ValueDate, true)
            {
                Subject = new ActorSubject(ActorType.Command, StartFuturesTickDataStreamingCommand.Actor, StartFuturesTickDataStreamingCommand.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = StartFuturesTickDataStreamingCommand.ErrorId
            },
            _ => throw new InvalidOperationException($"Unsupported event type: {e.GetType().FullName}")
        };

        var serviceResult = await context.RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <param name="entityId"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask StartFuturesBarDataStreamingAsync(this IEventActorContext context, IEvent e, FuturesBarDataStreamingId entityId)
    {
        StartFuturesBarDataStreamingCommand cmd = e switch
        {
            MarketDataFeedStartedCompleteEvent o => new(o.FuturesContracts!, o.ValueDate)
            {
                Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = StartFuturesBarDataStreamingCommand.ErrorId
            },
            MarketDataFeedResetCompleteEvent o => new(o.FuturesContracts!, o.ValueDate)
            {
                Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = StartFuturesBarDataStreamingCommand.ErrorId
            },
            _ => throw new InvalidOperationException($"Unsupported event type: {e.GetType().FullName}")
        };
        var serviceResult = await context.RequestAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    internal static async ValueTask SendResetStreamingEventAsync(this IEventActorContext context, MarketDataFeedResetCompleteEvent e)
    {
        var resetStreamingEvent = new MarketDataFeedResetStreamingEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedResetStreamingEvent.Actor, MarketDataFeedResetStreamingEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            CommandId = e.CommandId
        };
        await context.SendAsync<MarketDataFeedResetStreamingEvent, MarketDataFeedId>(resetStreamingEvent);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    internal static async ValueTask SendMarketDataFeedStartedCompleteAsync(
        this IEventActorContext context, MarketDataFeedStartedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<MarketDataFeedStartedCompleteEvent, MarketDataFeedId>() as MarketDataFeedStartedCompleteEvent;
        await context.SendAsync<MarketDataFeedStartedCompleteEvent, MarketDataFeedId>(completeEvent!);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <param name="ex"></param>
    /// <returns></returns>
    internal static async ValueTask SendMarketDataFeedStartedFailAsync(
        this IEventActorContext context, MarketDataFeedStartedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<MarketDataFeedStartedFailEvent, MarketDataFeedId>(ex) as MarketDataFeedStartedFailEvent;
        await context.SendAsync<MarketDataFeedStartedFailEvent, MarketDataFeedId>(failEvent!);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    internal static async ValueTask SendMarketDataFeedStoppedCompleteAsync(this IEventActorContext context, MarketDataFeedStoppedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<MarketDataFeedStoppedCompleteEvent, MarketDataFeedId>() as MarketDataFeedStoppedCompleteEvent;
        await context.SendAsync<MarketDataFeedStoppedCompleteEvent, MarketDataFeedId>(completeEvent!);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <param name="ex"></param>
    /// <returns></returns>
    internal static async ValueTask SendMarketDataFeedStoppedFailAsync(this IEventActorContext context, MarketDataFeedStoppedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<MarketDataFeedStoppedFailEvent, MarketDataFeedId>(ex) as MarketDataFeedStoppedFailEvent;
        await context.SendAsync<MarketDataFeedStoppedFailEvent, MarketDataFeedId>(failEvent!);
    }

    /// <summary>
    /// Turns on the live feed for a specific trade identified by the provided order and trade IDs.
    /// </summary>
    /// <param name="context">The event actor context used to process the command and interact with the event system.</param>
    /// <param name="commandId">The unique identifier for the command, used for tracking and correlation.</param>
    /// <param name="orderId">The identifier of the order associated with the trade. Must be a valid positive integer.</param>
    /// <param name="tradeId">The identifier of the trade for which to activate the live feed. Must be a valid positive integer.</param>
    /// <returns>A task that represents the asynchronous operation to enable the trade live feed.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service request to turn on the live feed fails.</exception>
    internal static async ValueTask TurnTradeLiveFeedOnAsync(this IEventActorContext context, Guid commandId, int orderId, int tradeId, DateOnly valueDate)
    {
        var entityId = new TradeLiveFeedId(orderId, tradeId, valueDate);
        TurnTradeLiveFeedOnCommand cmd = new(orderId, tradeId, valueDate)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, TurnTradeLiveFeedOnCommand.Actor, TurnTradeLiveFeedOnCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = TurnTradeLiveFeedOnCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<TurnTradeLiveFeedOnCommand, TradeLiveFeedId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    internal static async ValueTask<OptionTradeReadModel> GetOptionTradeQueryAsync(this IEventActorContext context, int orderId, int tradeId)
    {
        var entityId = new GetOptionTradeParameter(orderId, tradeId);
        GetOptionTradeQuery query = new(orderId, tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionTradeQuery.Actor, GetOptionTradeQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetOptionTradeQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<OptionTradeReadModel, GetOptionTradeQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }

    internal static async ValueTask SendTradeLiveFeedAddedFailEventAsync(this IEventActorContext context, TradeLiveFeedAddedEvent e, Exception ex)
    {
        var failEvent = e.ToFailEvent<TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(ex) as TradeLiveFeedAddedFailEvent;
        await context.SendAsync<TradeLiveFeedAddedFailEvent, TradeLiveFeedId>(failEvent!);
    }

    internal static async ValueTask<FuturesOptionContractReadModel> GetFuturesOptionContractAsync(this IEventActorContext context, string contractId)
    {
        var entityId = new GetFuturesOptionContractParameter(contractId);
        GetFuturesOptionContractQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesOptionContractQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }
    internal static async ValueTask<FuturesContractV2ReadModel> GetFuturesContractAsync(this IEventActorContext context, string contractId)
    {
        var entityId = new GetFuturesContractParameter(contractId);
        GetFuturesContractQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesContractQuery.Actor, GetFuturesContractQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesContractQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<FuturesContractV2ReadModel, GetFuturesContractQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }

    internal static async ValueTask StartFuturesOptionTickDataStreamingAsync(this IEventActorContext context, Guid commandId, FuturesOptionTickEntityId entityId, FuturesOptionContractReadModel contract, FuturesContractV2ReadModel baseContract, DateOnly valueDate, DateOnly maturityDate, double riskFreeRate)
    {
        StartFuturesOptionTickDataStreamingCommand cmd = new(entityId, contract, baseContract, valueDate, maturityDate, riskFreeRate)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = StartFuturesOptionTickDataStreamingCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<StartFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }


}

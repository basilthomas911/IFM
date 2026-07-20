using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

/// <summary>
/// Provides extension methods on <see cref="IEventActorContext"/> that encapsulate outgoing
/// messages (events, queries, and commands) originating from the futures-option tick-data
/// event actor.
/// </summary>
/// <remarks>
/// Each method constructs the appropriate actor message with its <see cref="ActorSubject"/>,
/// entity identifier, and payload, then dispatches it through the actor messaging
/// infrastructure. This keeps message-construction details out of the event handlers
/// themselves.
/// </remarks>
internal static class FuturesOptionTickDataEventExtensions
{
    /// <summary>
    /// Publishes an <see cref="OptionTradeTickPriceDataUpdatedEvent"/> derived from the
    /// specified tick-data insertion event.
    /// </summary>
    /// <remarks>The event is addressed using the contract identifier and value date
    /// extracted from the inserted tick data.</remarks>
    /// <param name="context">The event actor context used to send the event.</param>
    /// <param name="e">The source insertion event containing the tick data to propagate.</param>
    /// <returns>A value task that completes when the event has been dispatched.</returns>
    internal static async ValueTask SendOptionTradeTickPriceDataUpdatedEventAsync(this IEventActorContext context, FuturesOptionTickDataInsertedEvent e)
    {
        var entityId = new FuturesOptionTickEntityId(e.TickData.ContractId, e.TickData.ValueDate);
        OptionTradeTickPriceDataUpdatedEvent updatedEvent = new(e.TickData)
        {
            Subject = new ActorSubject(ActorType.Event, OptionTradeTickPriceDataUpdatedEvent.Actor, OptionTradeTickPriceDataUpdatedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            Id = Guid.NewGuid(),
            CommandId = e.CommandId,
            AggregateId = e.AggregateId,
            ReceivedOn = DateTime.UtcNow
        };
        await context.SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(updatedEvent);
    }
    /// <summary>
    /// Sends a completion event indicating that futures-option tick-data streaming was
    /// started successfully.
    /// </summary>
    /// <param name="context">The event actor context used to send the event.</param>
    /// <param name="e">The original streaming-started event from which the completion
    /// event is derived.</param>
    /// <returns>A value task that completes when the event has been dispatched.</returns>
    internal static async ValueTask SendFuturesOptionTickDataStreamingStartedCompleteAsync(this IEventActorContext context, FuturesOptionTickDataStreamingStartedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<FuturesOptionTickDataStreamingStartedCompleteEvent, FuturesOptionTickEntityId>() as FuturesOptionTickDataStreamingStartedCompleteEvent;
        await context.SendAsync<FuturesOptionTickDataStreamingStartedCompleteEvent, FuturesOptionTickEntityId>(completeEvent!);
    }

    /// <summary>
    /// Sends a completion event indicating that futures-option tick-data streaming was
    /// stopped successfully.
    /// </summary>
    /// <param name="context">The event actor context used to send the event.</param>
    /// <param name="e">The original streaming-stopped event from which the completion
    /// event is derived.</param>
    /// <returns>A value task that completes when the event has been dispatched.</returns>
    internal static async ValueTask SendFuturesOptionTickDataStreamingStoppedCompleteAsync(this IEventActorContext context, FuturesOptionTickDataStreamingStoppedEvent e)
    {
        var completeEvent = e.ToCompleteEvent<FuturesOptionTickDataStreamingStoppedCompleteEvent, FuturesOptionTickEntityId>() as FuturesOptionTickDataStreamingStoppedCompleteEvent;
        await context.SendAsync<FuturesOptionTickDataStreamingStoppedCompleteEvent, FuturesOptionTickEntityId>(completeEvent!);
    }

    /// <summary>
    /// Sends a failure event indicating that futures-option tick-data streaming could not
    /// be started.
    /// </summary>
    /// <param name="context">The event actor context used to send the event.</param>
    /// <param name="e">The original streaming-started event that triggered the failure.</param>
    /// <param name="ex">The exception that caused the streaming start to fail.</param>
    /// <returns>A value task that completes when the event has been dispatched.</returns>
    internal static async ValueTask SendFuturesOptionTickDataStreamingStartedFailAsync(this IEventActorContext context, FuturesOptionTickDataStreamingStartedEvent e, Exception ex)
    {
        var completeEvent = e.ToFailEvent<FuturesOptionTickDataStreamingStartedFailEvent, FuturesOptionTickEntityId>(ex) as FuturesOptionTickDataStreamingStartedFailEvent;
        await context.SendAsync<FuturesOptionTickDataStreamingStartedFailEvent, FuturesOptionTickEntityId>(completeEvent!);
    }

    /// <summary>
    /// Sends a failure event indicating that futures-option tick-data streaming could not
    /// be stopped.
    /// </summary>
    /// <param name="context">The event actor context used to send the event.</param>
    /// <param name="e">The original streaming-stopped event that triggered the failure.</param>
    /// <param name="ex">The exception that caused the streaming stop to fail.</param>
    /// <returns>A value task that completes when the event has been dispatched.</returns>
    internal static async ValueTask SendFuturesOptionTickDataStreamingStoppedFailAsync(this IEventActorContext context, FuturesOptionTickDataStreamingStoppedEvent e, Exception ex)
    {
        var completeEvent = e.ToFailEvent<FuturesOptionTickDataStreamingStoppedFailEvent, FuturesOptionTickEntityId>(ex) as FuturesOptionTickDataStreamingStoppedFailEvent;
        await context.SendAsync<FuturesOptionTickDataStreamingStoppedFailEvent, FuturesOptionTickEntityId>(completeEvent!);
    }

    /// <summary>
    /// Queries the market-data-feed actor for the streaming request identifier associated
    /// with the specified request key.
    /// </summary>
    /// <remarks>This method performs an asynchronous request via the actor messaging
    /// infrastructure. If the query fails or returns no data, the method returns
    /// <c>0</c>.</remarks>
    /// <param name="context">The event actor context used to issue the query.</param>
    /// <param name="requestKey">The key that uniquely identifies the streaming request
    /// (typically a contract identifier).</param>
    /// <returns>A value task whose result is the streaming request identifier, or <c>0</c>
    /// if the query was unsuccessful.</returns>
    internal static async ValueTask<int> GetStreamingRequestIdQueryAsync(this IEventActorContext context, string requestKey)
    {
        var streamingRequestId = 0;
        var entityId = new GetStreamingRequestIdParameter();
        GetStreamingRequestIdQuery query = new(requestKey)
        {
            Subject = new ActorSubject(ActorType.Query, GetStreamingRequestIdQuery.Actor, GetStreamingRequestIdQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetStreamingRequestIdQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<ScalarValue<int>, GetStreamingRequestIdQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            streamingRequestId = serviceResult.Value.Value;
        return streamingRequestId;
    }

    /// <summary>
    /// Sends a command to insert futures-option tick price data for the specified
    /// underlying contract and option tick data.
    /// </summary>
    /// <remarks>If the command fails, an <see cref="InvalidOperationException"/> is thrown
    /// containing the error message from the service result.</remarks>
    /// <param name="context">The event actor context used to send the command.</param>
    /// <param name="underlyingContract">The underlying futures contract associated with
    /// the option tick data.</param>
    /// <param name="optionContract">The option tick data to insert, including contract
    /// identifier, value date, and pricing information.</param>
    /// <returns>A value task that completes when the command has been processed
    /// successfully.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the command execution
    /// fails.</exception>
    internal static async ValueTask InsertFuturesOptionTickPriceDataAsync(this IEventActorContext context, FuturesContractV2ReadModel underlyingContract, FuturesOptionTickDataV2ReadModel optionContract)
    {
        var entityId = new FuturesOptionTickEntityId(optionContract.ContractId, optionContract.ValueDate);
        InsertFuturesOptionTickPriceDataCommand cmd = new(underlyingContract, optionContract)
        {
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickPriceDataCommand.Actor, InsertFuturesOptionTickPriceDataCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = InsertFuturesOptionTickPriceDataCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<InsertFuturesOptionTickPriceDataCommand, FuturesOptionTickEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

    /// <summary>
    /// Sends a command to insert a futures-option tick data record for the specified
    /// underlying contract and option tick data.
    /// </summary>
    /// <remarks>If the command fails, an <see cref="InvalidOperationException"/> is thrown
    /// containing the error message from the service result.</remarks>
    /// <param name="context">The event actor context used to send the command.</param>
    /// <param name="underlyingContract">The underlying futures contract associated with
    /// the option tick data.</param>
    /// <param name="optionContract">The option tick data to insert, including contract
    /// identifier, value date, and computed option greeks.</param>
    /// <returns>A value task that completes when the command has been processed
    /// successfully.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the command execution
    /// fails.</exception>
    public static async ValueTask InsertFuturesOptionTickDataAsync(this IEventActorContext context, FuturesContractV2ReadModel underlyingContract, FuturesOptionTickDataV2ReadModel optionContract)
    {
        var entityId = new FuturesOptionTickEntityId(optionContract.ContractId, optionContract.ValueDate);
        InsertFuturesOptionTickDataCommand cmd = new(underlyingContract, optionContract)
        {
            Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = InsertFuturesOptionTickDataCommand.ErrorId
        };
        var serviceResult = await context.RequestAsync<InsertFuturesOptionTickDataCommand, FuturesOptionTickEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

   /// <summary>
   /// Updates the option leg data for futures option trades based on the latest tick data and risk-free rate.
   /// </summary>
   /// <remarks>Only trades with valid states and option legs referencing the specified contract are updated.
   /// Updates are treated as intra-day and include the latest bid/ask prices, greeks, and underlying price. The method
   /// does not update trades if the input array is null or empty.</remarks>
   /// <param name="context">The event actor context used to dispatch the update command for option trade leg data.</param>
   /// <param name="optionTickData">The latest tick data for the option contract, including pricing and greeks, used to update the corresponding
   /// trade leg.</param>
   /// <param name="riskFreeRate">The risk-free interest rate applied in option pricing calculations for the updated leg data.</param>
   /// <param name="optionTrades">An array of option trades whose legs may reference the same contract as the provided tick data. Only valid trades
   /// with matching legs are updated.</param>
   /// <returns>A task that represents the asynchronous update operation.</returns>
   /// <exception cref="NotImplementedException">Thrown if the trade type is not supported for decomposition into a single-leg spread type.</exception>
    public static async ValueTask UpdateFuturesOptionTradeLegDataAsync(this IEventActorContext context, 
        FuturesOptionTickDataV2ReadModel optionTickData, 
        double riskFreeRate,  
        OptionTradeReadModel[] optionTrades)
    {
        // Retrieve all option trades whose legs reference the same contract as the incoming tick data.
        if (optionTrades is null || optionTrades.Length == 0) return;

        foreach (var optionTrade in optionTrades)
        {
            // Skip trades that are not in a valid state or have no option legs defined.
            if (!optionTrade.IsValid) continue;
            if (optionTrade.OptionLegs is null) continue;

            // Skip trades that do not contain a leg matching the tick data's contract identifier.
            if (optionTrade.OptionLegs.Any(o => o.ContractId == optionTickData.ContractId) != true) continue;

            // Locate the specific option leg within the trade that matches the tick data contract.
            var optionLeg = optionTrade.OptionLegs?.Where(o => o.ContractId == optionTickData.ContractId)?.SingleOrDefault();

            // Resolve the effective trade type by decomposing iron-condor strategies into their
            // constituent credit/debit spread types based on the option leg's put/call classification.
            var tradeType = GetTradePositionTradeType(optionTrade.TradeType, optionLeg!.OptionLegType);

            var valueDate = optionTickData.ValueDate;

            // Calculate the number of calendar days remaining until the option's maturity date.
            var daysToExpiry = optionTrade.MaturityDate.DayNumber - valueDate.DayNumber;

            // All updates from live tick data are treated as intra-day status.
            var tradeStatus = TradeStatus.IntraDay;

            // Build the option leg data read model from the latest tick data, including
            // current bid/ask prices and the full set of option greeks (IV, delta, gamma,
            // theta, vega, rho), then attach the original option leg definition.
            var optionLegData = new OptionTradeLegDataReadModel(
                        orderId: optionTrade.OrderId,
                        tradeId: optionTrade.TradeId,
                        tradeType: tradeType,
                        valueDate: valueDate,
                        daysToExpiry: daysToExpiry,
                        tradeStatus: tradeStatus,
                        optionLegId: optionLeg.ContractId,
                        bidPrice: Convert.ToDecimal(optionTickData.BidPrice),
                        askPrice: Convert.ToDecimal(optionTickData.AskPrice),
                        impliedVolatility: optionTickData.ImpliedVolatility,
                        delta: optionTickData.Delta,
                        gamma: optionTickData.Gamma,
                        theta: optionTickData.Theta,
                        vega: optionTickData.Vega,
                        rho: optionTickData.Rho,
                        createdOn: DateTime.Now,
                        createdBy: Environment.UserName,
                        updatedOn: DateTime.Now,
                        updatedBy: Environment.UserName
                    ).SetOptionLeg(optionLeg);

            // Dispatch the change command to persist the updated leg data, including the
            // underlying asset price and risk-free rate used for option pricing.
            await context.ChangeOptionTradeLegDataAsync(
                optionTrade.OrderId,
                optionTrade.TradeId,
                tradeType,
                valueDate,
                tradeStatus,
                Convert.ToDecimal(optionTickData.UnderlyingPrice),
                riskFreeRate,
                optionLegData);
        }

        // Maps composite iron-condor trade types to the appropriate single-leg spread type
        // based on whether the option leg is a put or call.
        TradeType GetTradePositionTradeType(TradeType tradeType,OptionType optionType)
             => tradeType switch
             {
                 TradeType.ShortIronCondor => optionType == OptionType.Put ? TradeType.PutCreditSpread : TradeType.CallCreditSpread,
                 TradeType.LongIronCondor => optionType == OptionType.Put ? TradeType.PutDebitSpread : TradeType.CallDebitSpread,
                 _ => throw new NotImplementedException()
             };
    }

    /// <summary>
    /// Sends a command to change/update an option trade leg's calculated data for a
    /// specific trade context and value date.
    /// </summary>
    /// <remarks>If the command fails, an <see cref="InvalidOperationException"/> is thrown
    /// containing the error message from the service result.</remarks>
    /// <param name="context">The event actor context used to send the command.</param>
    /// <param name="orderId">Order identifier for the option trade.</param>
    /// <param name="tradeId">Trade identifier within the order.</param>
    /// <param name="tradeType">Type of the option trade (strategy classification).</param>
    /// <param name="valueDate">Value (trading) date associated with the leg data.</param>
    /// <param name="tradeStatus">Status of the trade at the time the leg data was calculated.</param>
    /// <param name="assetPrice">Underlying asset price used for option calculations.</param>
    /// <param name="riskFreeRate">Risk-free interest rate used in option pricing.</param>
    /// <param name="optionLegData">Calculated option leg data payload.</param>
    /// <returns>A value task that completes when the command has been processed
    /// successfully.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the command execution
    /// fails.</exception>
    static async ValueTask ChangeOptionTradeLegDataAsync(
        this IEventActorContext context,
           int orderId,
           int tradeId,
           TradeType tradeType,
           DateOnly valueDate,
           TradeStatus tradeStatus,
           decimal assetPrice,
           double riskFreeRate,
           OptionTradeLegDataReadModel optionLegData)
    {
        var entityId = new OptionTradeEntityId(orderId, tradeId);
        ChangeOptionTradeLegDataCommand cmd = new(orderId, tradeId, tradeType, valueDate, tradeStatus, assetPrice, riskFreeRate, optionLegData)
        {
            Subject = new ActorSubject(ActorType.Command, ChangeOptionTradeLegDataCommand.Actor, ChangeOptionTradeLegDataCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serviceResult = await context.RequestAsync<ChangeOptionTradeLegDataCommand, OptionTradeEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }

}

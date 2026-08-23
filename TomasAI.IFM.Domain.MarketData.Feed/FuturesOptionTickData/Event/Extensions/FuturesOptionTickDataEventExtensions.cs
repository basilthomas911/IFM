using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

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
    public static async ValueTask UpdateFuturesOptionTradeLegDataAsync(this IActorTradeCommandApi commandApi,
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
            _ = await commandApi.ChangeOptionTradeLegDataAsync(
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

}

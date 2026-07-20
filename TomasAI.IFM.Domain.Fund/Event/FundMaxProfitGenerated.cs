using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Event;

public static class FundMaxProfitGeneratedEventHandler
{
    static readonly string ServiceId = "FundEventHandlers";

    /// <summary>
    /// Asynchronously processes the FundMaxProfitGeneratedEvent by retrieving necessary data, 
    /// calculating the fund's maximum profit and risk percentage, 
    /// and then sending a completion or failure event based on the outcome.
    /// </summary>
    /// <param name="e">The FundMaxProfitGeneratedEvent to process.</param>
    /// <param name="context">The event actor context used to send the completion event after processing.</param>
    /// <param name="logger">The logger used to record diagnostic and operational information.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FundMaxProfitGeneratedEvent e, IEventActorContext context, ILogger logger)
    {
        try
        {
            // get fund max profit genrated data...
            var fundMaxProfitGeneratedData = await context.GetFundMaxProfitGeneratedAsync(e.FundOrder.FundId, e.FundOrder.TradeDate);
            var fundBalance = fundMaxProfitGeneratedData.FundBalance;
            var profitOrders = fundMaxProfitGeneratedData.FundProfitOrders;
            var lossOrders = fundMaxProfitGeneratedData.FundLossOrders;
            var fundDrawdownBalances = fundMaxProfitGeneratedData.FundDrawdownBalances;

            // get futures trade signal...
            var tradeSignal = await context.GetFuturesTradeSignalAsync(e.FundOrder.BaseContractId, e.FundOrder.TradeDate);

            // set default fund risk from futures trade signal...
            var fundRiskPercent = 0.1;
            if (tradeSignal is not null)
                fundRiskPercent = tradeSignal.FundRiskPercent;

            // get fund win loss ratio for current month...
            var fundWinLossRatio = 0.0;
            var kellyCriteria = 0.0;
             if (lossOrders is not null && profitOrders is not null)
            {
                var lossCount = Convert.ToDouble(lossOrders.Count);
                var winCount = Convert.ToDouble(profitOrders.Count);
                var winRate = (winCount + lossCount) > 0 ? winCount / (winCount + lossCount) : 0;
                var lossRate = (winCount + lossCount) > 0 ? lossCount / (winCount + lossCount) : 0;
                var avgTradeProfit = Convert.ToDouble(profitOrders.Count > 0 ? profitOrders.Average(o => o.Amount) : 0);
                var avgTradeLoss = Convert.ToDouble(lossOrders.Count > 0 ? lossOrders.Average(o => o.Amount) : 0);
                var winRatio = winRate * avgTradeProfit;
                var lossRatio = lossRate * avgTradeLoss;
                fundWinLossRatio = lossRatio == 0 ? 0 : Math.Abs(winRatio / lossRatio);
                kellyCriteria = (lossRate * avgTradeProfit) == 0 ? 0 : winRate * Math.Abs(avgTradeLoss) / (lossRate * avgTradeProfit);
            }

            // calculate fund drawdown percent...
            var fundDrawdownPercent = 0.0;
            if (fundDrawdownBalances is not null)
                fundDrawdownPercent = (Convert.ToDouble(fundDrawdownBalances.EndBalance) - Convert.ToDouble(fundDrawdownBalances.StartBalance)) / Convert.ToDouble(fundDrawdownBalances.StartBalance);

            // if fund drawdown percent is < -5%, reduce fund risk percent by 50%...
            if (fundDrawdownPercent < -0.05)
                fundRiskPercent = fundRiskPercent * 0.50;

            // if fund win loss ratio is less than one, reduce fund risk percent by 25%...
            else if (fundWinLossRatio > 0.0 && fundWinLossRatio < 1.0)
                fundRiskPercent = fundRiskPercent * 0.75;

            // return minimum fund risk percent...
            fundRiskPercent = Math.Max(0.05, fundRiskPercent);

            // raise fund max profit generated complete event...
            var fundMaxProfit = (decimal)((double)fundBalance * fundRiskPercent);
            e.FundMaxProfit = new FundMaxProfitReadModel(
                fundOrderId: e.FundOrder.Id,
                fundMaxProfit: fundMaxProfit,
                fundRiskPercent: fundRiskPercent);

            var completeEvent = e.ToCompleteEvent<FundMaxProfitGeneratedCompleteEvent, FundId>() as FundMaxProfitGeneratedCompleteEvent;
            await context.SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(completeEvent!);
            logger.LogInformationEvent(ServiceId, "Processed FundMaxProfitGeneratedEvent for FundOrderId: {FundOrderId}, FundMaxProfit: {FundMaxProfit}, FundRiskPercent: {FundRiskPercent}", e.FundOrder.Id, fundMaxProfit, fundRiskPercent);
            return true;
        }
        catch (Exception ex)
        {
            var failEvent = e.ToFailEvent<FundMaxProfitGeneratedFailEvent, FundId>(ex) as FundMaxProfitGeneratedFailEvent;
            await context.SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(failEvent!);
            logger.LogErrorEvent(ServiceId, ex, "Error processing FundMaxProfitGeneratedEvent for FundOrderId: {FundOrderId}", e.FundOrder.Id);
            return false;
        }
    }

    /// <summary>
    /// Asynchronously retrieves the maximum profit generated for a specific fund and trade date by sending a query through the event actor context.
    /// </summary>
    /// <param name="context">The event actor context.</param>
    /// <param name="fundId">The fund ID.</param>
    /// <param name="tradeDate">The trade date.</param>
    /// <returns>The fund maximum profit generated read model.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the query fails.</exception>
    static async ValueTask<FundMaxProfitGeneratedReadModel> GetFundMaxProfitGeneratedAsync(
        this IEventActorContext context,
        int fundId,
        DateOnly tradeDate)
    {
        var entityId = new GetFundMaxProfitGeneratedParameter(fundId, tradeDate);
        GetFundMaxProfitGeneratedQuery query = new(fundId, tradeDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFundMaxProfitGeneratedQuery.Actor, GetFundMaxProfitGeneratedQuery.Verb, entityId.Format()),
        };
        var serviceResult = await context.RequestAsync<FundMaxProfitGeneratedReadModel, GetFundMaxProfitGeneratedQuery>(query);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
        return serviceResult.Value;
    }

    /// <summary>
    /// Asynchronously retrieves the futures trade signal for a specific base contract ID and trade date by sending a query through the event actor context.
    /// </summary>
    /// <param name="context">The event actor context.</param>
    /// <param name="baseContractId">The base contract ID.</param>
    /// <param name="tradeDate">The trade date.</param>
    /// <returns>The futures trade signal read model.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the query fails.</exception>
    static async ValueTask<FuturesTradeSignalV2ReadModel?> GetFuturesTradeSignalAsync(
        this IEventActorContext context,
        string baseContractId,
        DateOnly tradeDate)
    {
        var entityId = new GetFuturesTradeSignalParameter(baseContractId, tradeDate);
        GetFuturesTradeSignalQuery query = new(baseContractId, tradeDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTradeSignalQuery.Actor, GetFuturesTradeSignalQuery.Verb, entityId.Format()),
        };
        var serviceResult = await context.RequestAsync<FuturesTradeSignalV2ReadModel, GetFuturesTradeSignalQuery>(query);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
        return serviceResult.Value;

    }
}

using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// create market data analytics command api
/// </summary>
/// <param name="commandSvc"></param>
public class MarketDataAnalyticsCommandApi(ICommandService commandSvc) : IMarketDataAnalyticsCommandApi
{
    const string MarketDataAnalyticsController = "MarketDataAnalytics";

    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// start futures rsi signal service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId)
        => await new StartFuturesRsiSignalCommand(entityId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// stop futures rsi signal service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId)
        => await new StopFuturesRsiSignalCommand(entityId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// generate futures rsi signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(FuturesEodDataV2ReadModel futuresEodData) 
        => await new GenerateFuturesRsiSignalCommand(futuresEodData)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// generate futures rsi daily signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiDailySignalAsync(FuturesEodDataV2ReadModel futuresEodData)
        => await new GenerateFuturesRsiDailySignalCommand( futuresEodData)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// update futures trade signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <param name="futuresRsiSignal"></param>
    /// <param name="futuresTdiSignal"></param>
    /// <param name="futuresItiSignal"></param>
    /// <param name="vixFuturesPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateFuturesTradeSignalAsync(
        FuturesEodDataV2ReadModel futuresEodData, 
        FuturesRsiSignalReadModel futuresRsiSignal, 
        FuturesTdiSignalReadModel futuresTdiSignal,
        FuturesItiSignalDataReadModel futuresItiSignalData,
        decimal vixFuturesPrice)
        => await new UpdateFuturesTradeSignalCommand( futuresEodData, futuresRsiSignal, futuresTdiSignal, futuresItiSignalData, vixFuturesPrice)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// generate futures trend direction indicator
    /// </summary>
    /// <param name="futuresTdiSignalId"></param>
    /// <param name="futuresRsiSignals"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(FuturesTdiSignalId futuresTdiSignalId, FuturesRsiSignalReadModel[] futuresRsiSignals)
         => await new GenerateFuturesTdiSignalCommand ( futuresTdiSignalId, futuresRsiSignals )
         .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// generate futures trade signal LLM data
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <param name="priceVolatility"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesTradeSignalLLMAsync(FuturesEodDataV2ReadModel futuresEodData, double priceVolatility)
         => await new GenerateFuturesTradeSignalLLMCommand( futuresEodData, priceVolatility)
         .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// start futures trade signal LLM service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<ServiceResult<Guid>> StartFuturesTradeSignalLLMAsync(FuturesTradeSignalId entityId)
         => await new StartFuturesTradeSignalLLMCommand( entityId)
         .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// stop futures trade signal LLM service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<ServiceResult<Guid>> StopFuturesTradeSignalLLMAsync(FuturesTradeSignalId entityId)
         => await new StopFuturesTradeSignalLLMCommand(entityId)
         .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    /// generate futures iti signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timestamp"></param>
    /// <param name="symbol"></param>
    /// <param name="futuresPrice"></param>
    /// <param name="lambda"></param>
    /// <param name="futuresPercentChange"></param>
    /// <param name="futuresMean"></param>
    /// <param name="futuresStdDev"></param>
    /// <param name="futuresMDI"></param>
    /// <param name="futuresRSI"></param>
    /// <param name="futuresRSISlope"></param>
    /// <param name="futuresFiftyDMA"></param>
    /// <param name="futuresTwoHundredDMA"></param>
    /// <param name="futuresItiMDIDistribution"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesItiSignalAsync(
        string contractId, DateOnly valueDate, DateTime timestamp, string symbol, double futuresPrice, double lambda, double futuresPercentChange,
        double futuresMean, double futuresStdDev, double futuresMDI, double futuresRSI, double futuresRSISlope, decimal futuresFiftyDMA, decimal futuresTwoHundredDMA,
        FuturesItiMDIDistributionReadModel? futuresItiMDIDistribution, double predictedTrendDelta, FuturesItiTrendCoastLineCountersReadModel? coastLineCounters)
        => await new GenerateFuturesItiSignalCommand (
            contractId,
            valueDate, 
            timestamp, 
            symbol,
            futuresPrice, 
            lambda,
            futuresPercentChange,
            futuresMean,
            futuresStdDev,
            futuresMDI,
            futuresRSI,
            futuresRSISlope,
            futuresFiftyDMA,
            futuresTwoHundredDMA,
            futuresItiMDIDistribution,
            predictedTrendDelta,
            coastLineCounters!)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    ///  set futures iti signal hold trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SetFuturesItiSignalHoldTradeAsync(FuturesItiSignalId e)
       => await new SetFuturesItiSignalHoldTradeCommand ( e.ContractId, e.ValueDate, e.IntrinsicTime)
       .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

    /// <summary>
    ///  clear futures iti signal hold trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ClearFuturesItiSignalHoldTradeAsync(FuturesItiSignalId e)
        => await new ClearFuturesItiSignalHoldTradeCommand (e.ContractId, e.ValueDate, e.IntrinsicTime)
       .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataAnalyticsController));

}

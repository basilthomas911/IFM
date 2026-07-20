using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// create market data analytics command api
/// </summary>
/// <param name="commandSvc"></param>
public class MarketDataAnalyticsCommandApi(ICommandServiceApi commandSvc) 
    : IMarketDataAnalyticsCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// start futures rsi signal service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId)
        => await new StartFuturesRsiSignalParameter(IsArgumentNull.Set(entityId), StartFuturesRsiSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StartFuturesRsiSignal, e));

    /// <summary>
    /// stop futures rsi signal service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId)
        => await new StopFuturesRsiSignalParameter(IsArgumentNull.Set(entityId), StopFuturesRsiSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StopFuturesRsiSignal, e));

    /// <summary>
    /// generate futures rsi signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(FuturesEodDataV2ReadModel futuresEodData, TradeTimePeriodType timePeriod, int periodLength) 
        => await new GenerateFuturesRsiSignalParameter(IsArgumentNull.Set(futuresEodData), timePeriod, periodLength, GenerateFuturesRsiSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesRsiSignal, e));

    /// <summary>
    /// generate futures rsi daily signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiDailySignalAsync(FuturesEodDataV2ReadModel futuresEodData, TradeTimePeriodType timePeriod, int periodLength)
        => await new GenerateFuturesRsiDailySignalParameter(IsArgumentNull.Set(futuresEodData), timePeriod, periodLength, GenerateFuturesRsiDailySignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesRsiDailySignal, e));

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
        => await new UpdateFuturesTradeSignalParameter(futuresEodData, futuresRsiSignal, futuresTdiSignal, futuresItiSignalData, vixFuturesPrice, UpdateFuturesTradeSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.UpdateFuturesTradeSignal, e));

    /// <summary>
    /// generate futures trend direction indicator
    /// </summary>
    /// <param name="futuresTdiSignalId"></param>
    /// <param name="futuresRsiSignals"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(FuturesTdiSignalId futuresTdiSignalId, FuturesRsiSignalReadModel[] futuresRsiSignals)
        => await new GenerateFuturesTdiSignalParameter(IsArgumentNull.Set(futuresTdiSignalId), IsArgumentNull.Set(futuresRsiSignals), GenerateFuturesTdiSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesTdiSignal, e));

    /// <summary>
    /// generate futures iti signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="timestamp"></param>
    /// <param name="futuresPrice"></param>
    /// <param name="vixFuturesPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesItiSignalAsync(
        string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, DateTime timestamp, double futuresPrice, double vixFuturesPrice)
        => await new GenerateFuturesItiSignalParameter(
            contractId, valueDate, timePeriod, timestamp, futuresPrice, vixFuturesPrice, GenerateFuturesItiSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesItiSignal, e));

    /// <summary>
    ///  set futures iti signal hold trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SetFuturesItiSignalHoldTradeAsync(FuturesItiSignalId e)
        => await new SetFuturesItiSignalHoldTradeParameter(IsArgumentNull.Set(e), SetFuturesItiSignalHoldTradeCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.SetFuturesItiSignalHoldTrade, e));

    /// <summary>
    ///  clear futures iti signal hold trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ClearFuturesItiSignalHoldTradeAsync(FuturesItiSignalId e)
        => await new ClearFuturesItiSignalHoldTradeParameter(IsArgumentNull.Set(e), ClearFuturesItiSignalHoldTradeCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.ClearFuturesItiSignalHoldTrade, e));

    /// <summary>
    /// generate futures atr signal
    /// </summary>
    /// <param name="futuresAtrSignalId"></param>
    /// <param name="futuresItiSignals"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesAtrSignalAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesItiSignalV2ReadModel[] futuresItiSignals)
        => await new GenerateFuturesAtrSignalParameter(IsArgumentNull.Set(futuresAtrSignalId), IsArgumentNull.Set(futuresItiSignals), GenerateFuturesAtrSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesAtrSignal, e));

    /// <summary>
    /// generate futures atr signal from intra-day data
    /// </summary>
    /// <param name="futuresAtrSignalId"></param>
    /// <param name="futuresIntraDayData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesAtrSignalFromIntraDayDataAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesIntraDayDataReadModel[] futuresIntraDayData)
        => await new GenerateFuturesAtrSignalFromIntraDayDataParameter(IsArgumentNull.Set(futuresAtrSignalId), IsArgumentNull.Set(futuresIntraDayData), GenerateFuturesAtrSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesAtrSignalFromIntraDayData, e));

    /// <summary>
    /// generate futures ADX signal
    /// </summary>
    /// <param name="futuresAdxSignalId"></param>
    /// <param name="futuresPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesAdxSignalAsync(FuturesAdxSignalId futuresAdxSignalId, decimal futuresPrice)
        => await new GenerateFuturesAdxSignalParameter(IsArgumentNull.Set(futuresAdxSignalId), futuresPrice, GenerateFuturesAdxSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesAdxSignal, e));

    /// <summary>
    /// generate futures MACD signal
    /// </summary>
    /// <param name="futuresMacdSignalId"></param>
    /// <param name="futuresPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesMacdSignalAsync(FuturesMacdSignalId futuresMacdSignalId, decimal futuresPrice)
        => await new GenerateFuturesMacdSignalParameter(IsArgumentNull.Set(futuresMacdSignalId), futuresPrice, GenerateFuturesMacdSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesMacdSignal, e));

}

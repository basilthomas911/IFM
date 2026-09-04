using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
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

    /// <inheritdoc />
    public Task<ServiceResult<Guid>> EnsureHistoricalAnalyticsWarmupAsync(
        DateOnly candidateValueDate,
        string analyticsTargetContractId,
        Guid processBootId = default,
        Guid startupCommandId = default)
        => Task.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(
            26020,
            "Automatic historical warm-up is available only through the NATS actor API."));

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

    public async Task<ServiceResult<Guid>> StartFuturesMacdSignalAsync(FuturesMacdSignalEntityId entityId)
        => await new StartFuturesMacdSignalParameter(IsArgumentNull.Set(entityId), StartFuturesMacdSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StartFuturesMacdSignal, e));
    public async Task<ServiceResult<Guid>> StopFuturesMacdSignalAsync(FuturesMacdSignalEntityId entityId)
        => await new StopFuturesMacdSignalParameter(IsArgumentNull.Set(entityId), StopFuturesMacdSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StopFuturesMacdSignal, e));
    public async Task<ServiceResult<Guid>> StartFuturesAdxSignalAsync(FuturesAdxSignalEntityId entityId)
        => await new StartFuturesAdxSignalParameter(IsArgumentNull.Set(entityId), StartFuturesAdxSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StartFuturesAdxSignal, e));
    public async Task<ServiceResult<Guid>> StopFuturesAdxSignalAsync(FuturesAdxSignalEntityId entityId)
        => await new StopFuturesAdxSignalParameter(IsArgumentNull.Set(entityId), StopFuturesAdxSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StopFuturesAdxSignal, e));
    public async Task<ServiceResult<Guid>> StartFuturesAtrSignalAsync(FuturesAtrSignalEntityId entityId)
        => await new StartFuturesAtrSignalParameter(IsArgumentNull.Set(entityId), StartFuturesAtrSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StartFuturesAtrSignal, e));
    public async Task<ServiceResult<Guid>> StopFuturesAtrSignalAsync(FuturesAtrSignalEntityId entityId)
        => await new StopFuturesAtrSignalParameter(IsArgumentNull.Set(entityId), StopFuturesAtrSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.StopFuturesAtrSignal, e));

    /// <summary>
    /// generate futures rsi signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(FuturesEodDataV2ReadModel futuresEodData, TimeFrameType timePeriod, int periodLength) 
        => await new GenerateFuturesRsiSignalParameter(IsArgumentNull.Set(futuresEodData), timePeriod, periodLength, GenerateFuturesRsiSignalCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataAnalyticsUriPath.GenerateFuturesRsiSignal, e));

    /// <summary>
    /// generate futures rsi daily signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiDailySignalAsync(FuturesEodDataV2ReadModel futuresEodData, TimeFrameType timePeriod, int periodLength)
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
    public async Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(
        FuturesTdiSignalId futuresTdiSignalId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        FuturesTdiConfiguration? configuration = null)
        => await (new GenerateFuturesTdiSignalParameter(IsArgumentNull.Set(futuresTdiSignalId), IsArgumentNull.Set(futuresRsiSignals), GenerateFuturesTdiSignalCommand.ErrorId)
            { Configuration = configuration ?? FuturesTdiConfiguration.Standard })
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
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, DateTime timestamp, double futuresPrice, double vixFuturesPrice)
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

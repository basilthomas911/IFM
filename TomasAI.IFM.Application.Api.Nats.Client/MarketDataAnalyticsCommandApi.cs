using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// create market data analytics command api
/// </summary>
/// <param name="actorProducer"></param>
public class MarketDataAnalyticsCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IMarketDataAnalyticsCommandApi
{
    /// <summary>
    /// start futures rsi signal service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new StartFuturesRsiSignalCommand(entityId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StartFuturesRsiSignalCommand.Actor, StartFuturesRsiSignalCommand.Verb, entityId.Format()),
                ErrorCode = StartFuturesRsiSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StartFuturesRsiSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// stop futures rsi signal service
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StopFuturesRsiSignalAsync(FuturesRsiSignalEntityId entityId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new StopFuturesRsiSignalCommand(entityId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StopFuturesRsiSignalCommand.Actor, StopFuturesRsiSignalCommand.Verb, entityId.Format()),
                ErrorCode = StopFuturesRsiSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StopFuturesRsiSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// generate futures rsi signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(FuturesEodDataV2ReadModel futuresEodData, TradeTimePeriodType timePeriod, int periodLength)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesRsiSignalEntityId(futuresEodData.ContractId, futuresEodData.ValueDate, timePeriod, periodLength);
            var futuresRsiSignalId = new FuturesRsiSignalId(futuresEodData.ContractId, futuresEodData.ValueDate, timePeriod, periodLength, TimeOnly.MinValue);
            GenerateFuturesRsiSignalCommand cmd = new (futuresRsiSignalId, futuresEodData.ClosePrice);
            cmd = cmd with
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesRsiSignalCommand.Actor, GenerateFuturesRsiSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesRsiSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesRsiSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// generate futures rsi daily signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength" ></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiDailySignalAsync(FuturesEodDataV2ReadModel futuresEodData, TradeTimePeriodType timePeriod, int periodLength)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var futuresRsiSignalId = new FuturesRsiSignalId(futuresEodData.ContractId, futuresEodData.ValueDate, timePeriod, periodLength, TimeOnly.MinValue);
            var cmd = new GenerateFuturesRsiDailySignalCommand(futuresRsiSignalId, futuresEodData.ClosePrice);
            var entityId = cmd.EntityId;
            cmd = cmd with
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesRsiDailySignalCommand.Actor, GenerateFuturesRsiDailySignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesRsiDailySignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesRsiDailySignalCommand.ErrorId);
        }
        return serviceResult;
    }

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
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesTradeSignalEntityId(futuresEodData.ContractId ?? string.Empty, futuresEodData.ValueDate, TradeTimePeriodType.Daily);
            var cmd = new UpdateFuturesTradeSignalCommand(futuresEodData, futuresRsiSignal, futuresTdiSignal, futuresItiSignalData, vixFuturesPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, UpdateFuturesTradeSignalCommand.Actor, UpdateFuturesTradeSignalCommand.Verb, entityId.Format()),
                ErrorCode = UpdateFuturesTradeSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, UpdateFuturesTradeSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// generate futures trend direction indicator
    /// </summary>
    /// <param name="futuresTdiSignalId"></param>
    /// <param name="futuresRsiSignals"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(FuturesTdiSignalId futuresTdiSignalId, FuturesRsiSignalReadModel[] futuresRsiSignals)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesTdiSignalEntityId(futuresTdiSignalId.ContractId, futuresTdiSignalId.ValueDate, TradeTimePeriodType.Daily);
            GenerateFuturesTdiSignalCommand cmd = new (futuresTdiSignalId, futuresRsiSignals)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesTdiSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesTdiSignalCommand.ErrorId);
        }
        return serviceResult;
    }

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
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesItiSignalEntityId(contractId, valueDate, timePeriod);
            GenerateFuturesItiSignalCommand cmd = new (contractId, valueDate, timePeriod, timestamp, futuresPrice, vixFuturesPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesItiSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesItiSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    ///  set futures iti signal hold trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SetFuturesItiSignalHoldTradeAsync(FuturesItiSignalId e)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesItiSignalEntityId(e.ContractId, e.ValueDate, e.TimePeriod);
            SetFuturesItiSignalHoldTradeCommand cmd = new (e.ContractId, e.ValueDate, e.TimePeriod, e.IntrinsicTime)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, SetFuturesItiSignalHoldTradeCommand.Actor, SetFuturesItiSignalHoldTradeCommand.Verb, entityId.Format()),
                ErrorCode = SetFuturesItiSignalHoldTradeCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, SetFuturesItiSignalHoldTradeCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    ///  clear futures iti signal hold trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ClearFuturesItiSignalHoldTradeAsync(FuturesItiSignalId e)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesItiSignalEntityId(e.ContractId, e.ValueDate, e.TimePeriod);
            ClearFuturesItiSignalHoldTradeCommand cmd = new (e.ContractId, e.ValueDate, e.TimePeriod, e.IntrinsicTime)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ClearFuturesItiSignalHoldTradeCommand.Actor, ClearFuturesItiSignalHoldTradeCommand.Verb, entityId.Format()),
                ErrorCode = ClearFuturesItiSignalHoldTradeCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ClearFuturesItiSignalHoldTradeCommand.ErrorId);
        }
        return serviceResult;
    }


    /// <summary>
    /// generate futures atr signal
    /// </summary>
    /// <param name="futuresAtrSignalId"></param>
    /// <param name="futuresItiSignals"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesAtrSignalAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesItiSignalV2ReadModel[] futuresItiSignals)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult = new();
        /*
        try
        {
            var cmd = new GenerateFuturesAtrSignalCommand(futuresAtrSignalId, futuresItiSignals)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, cmd.EntityId.Format()),
                ErrorCode = GenerateFuturesAtrSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesAtrSignalCommand.ErrorId);
        }
        */
        return serviceResult;
    }

    /// <summary>
    /// generate futures atr signal from intra-day data
    /// </summary>
    /// <param name="futuresAtrSignalId"></param>
    /// <param name="futuresIntraDayData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesAtrSignalFromIntraDayDataAsync(FuturesAtrSignalId futuresAtrSignalId, FuturesIntraDayDataReadModel[] futuresIntraDayData)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = futuresAtrSignalId.ToEntityId();
            var futuresPrice = futuresIntraDayData.Length > 0 ? futuresIntraDayData[^1].ClosePrice : 0m;
            GenerateFuturesAtrSignalCommand cmd = new (futuresAtrSignalId, futuresPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesAtrSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesAtrSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// generate futures ADX signal
    /// </summary>
    /// <param name="futuresAdxSignalId"></param>
    /// <param name="futuresPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesAdxSignalAsync(FuturesAdxSignalId futuresAdxSignalId, decimal futuresPrice)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = futuresAdxSignalId.ToEntityId();
            GenerateFuturesAdxSignalCommand cmd = new (futuresAdxSignalId, futuresPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesAdxSignalCommand.Actor, GenerateFuturesAdxSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesAdxSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesAdxSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// generate futures MACD signal
    /// </summary>
    /// <param name="signalId"></param>
    /// <param name="futuresPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesMacdSignalAsync(FuturesMacdSignalId signalId, decimal futuresPrice)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesMacdSignalEntityId(signalId.ContractId, signalId.ValueDate, signalId.TimePeriod, signalId.PeriodLength);
            GenerateFuturesMacdSignalCommand cmd = new(signalId, futuresPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesMacdSignalCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesMacdSignalCommand.ErrorId);
        }
        return serviceResult;
    }

}

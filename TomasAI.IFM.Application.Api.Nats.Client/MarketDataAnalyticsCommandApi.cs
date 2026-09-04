using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// create market data analytics command api
/// </summary>
/// <param name="actorProducer"></param>
public class MarketDataAnalyticsCommandApi(IActorProducer actorProducer)
    : NatsClientApi(actorProducer), IMarketDataAnalyticsCommandApi
{
    /// <inheritdoc />
    public async Task<ServiceResult<Guid>> EnsureHistoricalAnalyticsWarmupAsync(
        DateOnly candidateValueDate,
        string analyticsTargetContractId,
        Guid processBootId = default,
        Guid startupCommandId = default)
    {
        var commandId = Guid.NewGuid();
        var entityId = new FuturesAnalyticsHistoricalDataLoaderEntityId(commandId);
        try
        {
            var command = new LoadFuturesAnalyticsHistoricalDataCommand
            {
                CommandId = commandId,
                EntityId = entityId,
                Subject = new(
                    ActorType.Command,
                    LoadFuturesAnalyticsHistoricalDataCommand.Actor,
                    LoadFuturesAnalyticsHistoricalDataCommand.Verb,
                    entityId.Format()),
                Parameters = new FuturesAnalyticsHistoricalDataLoaderParameters
                {
                    StartDate = candidateValueDate.AddYears(-1),
                    EndDate = candidateValueDate,
                    Series = [Series("ES", "calendar-front")],
                    SignalFamilies = ["EMA", "BollingerBand"],
                    MaximumCostUsd = 10m,
                    MaximumBytes = 1_073_741_824,
                    NormalizationVersion = "historical-daily-v1",
                    CalculationConfigurationVersion = "ema-bb-daily-v1",
                    RequestedBy = $"{Environment.UserDomainName}\\{Environment.UserName}",
                    AutomaticStartupWarmup = true,
                    AnalyticsTargetContractId = analyticsTargetContractId,
                    ProcessBootId = processBootId,
                    StartupCommandId = startupCommandId
                }
            };
            return await RequestCommandAsync(command, entityId).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return OnError(exception, commandId, LoadFuturesAnalyticsHistoricalDataCommand.ErrorId);
        }

        static FuturesAnalyticsHistorySeriesRequest Series(string root, string rollRule) => new()
        {
            MarketSeriesIdentity = MarketSeriesIdentity.ForFuturesSeries(
                new FuturesSeriesId(root, rollRule, "unadjusted", 1)),
            Schema = FuturesAnalyticsHistoricalSchema.OhlcvDaily
        };
    }

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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StopFuturesRsiSignalCommand.ErrorId);
        }
        return serviceResult;
    }

    public Task<ServiceResult<Guid>> StartFuturesMacdSignalAsync(FuturesMacdSignalEntityId entityId)
        => SendLifecycleAsync(entityId, StartFuturesMacdSignalCommand.ErrorId, commandId => new StartFuturesMacdSignalCommand(entityId)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StartFuturesMacdSignalCommand.Actor, StartFuturesMacdSignalCommand.Verb, entityId.Format())
        });

    public Task<ServiceResult<Guid>> StopFuturesMacdSignalAsync(FuturesMacdSignalEntityId entityId)
        => SendLifecycleAsync(entityId, StopFuturesMacdSignalCommand.ErrorId, commandId => new StopFuturesMacdSignalCommand(entityId)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StopFuturesMacdSignalCommand.Actor, StopFuturesMacdSignalCommand.Verb, entityId.Format())
        });

    public Task<ServiceResult<Guid>> StartFuturesAdxSignalAsync(FuturesAdxSignalEntityId entityId)
        => SendLifecycleAsync(entityId, StartFuturesAdxSignalCommand.ErrorId, commandId => new StartFuturesAdxSignalCommand(entityId)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StartFuturesAdxSignalCommand.Actor, StartFuturesAdxSignalCommand.Verb, entityId.Format())
        });

    public Task<ServiceResult<Guid>> StopFuturesAdxSignalAsync(FuturesAdxSignalEntityId entityId)
        => SendLifecycleAsync(entityId, StopFuturesAdxSignalCommand.ErrorId, commandId => new StopFuturesAdxSignalCommand(entityId)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StopFuturesAdxSignalCommand.Actor, StopFuturesAdxSignalCommand.Verb, entityId.Format())
        });

    public Task<ServiceResult<Guid>> StartFuturesAtrSignalAsync(FuturesAtrSignalEntityId entityId)
        => SendLifecycleAsync(entityId, StartFuturesAtrSignalCommand.ErrorId, commandId => new StartFuturesAtrSignalCommand(entityId)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StartFuturesAtrSignalCommand.Actor, StartFuturesAtrSignalCommand.Verb, entityId.Format())
        });

    public Task<ServiceResult<Guid>> StopFuturesAtrSignalAsync(FuturesAtrSignalEntityId entityId)
        => SendLifecycleAsync(entityId, StopFuturesAtrSignalCommand.ErrorId, commandId => new StopFuturesAtrSignalCommand(entityId)
        {
            CommandId = commandId,
            Subject = new ActorSubject(ActorType.Command, StopFuturesAtrSignalCommand.Actor, StopFuturesAtrSignalCommand.Verb, entityId.Format())
        });

    async Task<ServiceResult<Guid>> SendLifecycleAsync<TEntityId, TCommand>(
        TEntityId entityId, int errorCode, Func<Guid, TCommand> createCommand)
        where TEntityId : IActorEntityId
        where TCommand : class, ICommand<TEntityId>
    {
        var commandId = Guid.NewGuid();
        try
        {
            return await RequestCommandAsync(createCommand(commandId), entityId);
        }
        catch (Exception ex)
        {
            return OnError(ex, commandId, errorCode);
        }
    }

    /// <summary>
    /// generate futures rsi signal
    /// </summary>
    /// <param name="futuresEodData"></param>
    /// <param name="timePeriod"></param>
    /// <param name="periodLength"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiSignalAsync(FuturesEodDataV2ReadModel futuresEodData, TimeFrameType timePeriod, int periodLength)
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
    public async Task<ServiceResult<Guid>> GenerateFuturesRsiDailySignalAsync(FuturesEodDataV2ReadModel futuresEodData, TimeFrameType timePeriod, int periodLength)
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
            var cmd = new UpdateFuturesTradeSignalCommand(futuresEodData, futuresRsiSignal, futuresTdiSignal, futuresItiSignalData, vixFuturesPrice);
            cmd = cmd with
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, UpdateFuturesTradeSignalCommand.Actor, UpdateFuturesTradeSignalCommand.Verb, cmd.EntityId.Format()),
                ErrorCode = UpdateFuturesTradeSignalCommand.ErrorId
            };
            serviceResult = await RequestCommandAsync(cmd!, cmd.EntityId);
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
    public async Task<ServiceResult<Guid>> GenerateFuturesTdiSignalAsync(
        FuturesTdiSignalId futuresTdiSignalId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        FuturesTdiConfiguration? configuration = null)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            configuration ??= FuturesTdiConfiguration.Standard;
            var timePeriod = futuresTdiSignalId.TimePeriod;
            var entityId = new FuturesTdiSignalEntityId(
                futuresTdiSignalId.ContractId,
                futuresTdiSignalId.ValueDate,
                timePeriod,
                configuration.ConfigurationId);
            GenerateFuturesTdiSignalCommand cmd = new (futuresTdiSignalId, futuresRsiSignals, configuration)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesTdiSignalCommand.Actor, GenerateFuturesTdiSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesTdiSignalCommand.ErrorId
            };
            serviceResult = await RequestCommandAsync(cmd!, cmd.EntityId);
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
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, DateTime timestamp, double futuresPrice, double vixFuturesPrice)
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = futuresAtrSignalId.ToEntityId();
            var futuresPrice = futuresItiSignals.Length > 0
                ? (decimal)futuresItiSignals[^1].IntrinsicPrice
                : 0m;
            var cmd = new GenerateFuturesAtrSignalCommand(futuresAtrSignalId, futuresPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesAtrSignalCommand.ErrorId
            };
            serviceResult = await RequestCommandAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesAtrSignalCommand.ErrorId);
        }
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
            serviceResult = await RequestCommandAsync(cmd!, entityId);
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
            var entityId = signalId.ToEntityId();
            GenerateFuturesMacdSignalCommand cmd = new(signalId, futuresPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFuturesMacdSignalCommand.ErrorId
            };
            serviceResult = await RequestCommandAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFuturesMacdSignalCommand.ErrorId);
        }
        return serviceResult;
    }

}

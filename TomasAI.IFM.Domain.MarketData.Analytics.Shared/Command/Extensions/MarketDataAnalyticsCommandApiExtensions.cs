using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

/// <summary>
/// Sends Market Data Analytics commands from a running event actor and returns their typed replies.
/// </summary>
/// <remarks>
/// Each operation constructs the command subject, entity identity, and command error code before using
/// <see cref="IEventActorContext.RequestAsync{TCommand,TEntityId}(TCommand)"/>. The instance captures one
/// actor context supplied to each extension call.
/// </remarks>
public static class MarketDataAnalyticsCommandApiExtensions
{
    /// <summary>Sends one live exact-trade observation to the event-sourced VWAP actor.</summary>
    public static ValueTask<ServiceResult<GuidResult>> UpdateFuturesVwapSignalAsync(
        this IEventActorContext context,
        FuturesVwapSignalEntityId entityId,
        FuturesVwapTradeObservation observation,
        FuturesVwapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(configuration);
        UpdateFuturesVwapSignalCommand command = new()
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, UpdateFuturesVwapSignalCommand.Actor,
                UpdateFuturesVwapSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Observation = observation,
            Configuration = configuration
        };
        return RequestAsync<UpdateFuturesVwapSignalCommand, FuturesVwapSignalEntityId>(context, command);
    }

    /// <summary>Sends one immutable VX leg observation to the event-sourced term-structure actor.</summary>
    public static ValueTask<ServiceResult<GuidResult>> UpdateFuturesVxTermStructureSignalAsync(
        this IEventActorContext context,
        FuturesVxTermStructureSignalEntityId entityId,
        FuturesVxTermStructureLegObservation observation,
        FuturesVxTermStructureConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(configuration);
        UpdateFuturesVxTermStructureSignalCommand command = new()
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, UpdateFuturesVxTermStructureSignalCommand.Actor,
                UpdateFuturesVxTermStructureSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Observation = observation,
            Configuration = configuration
        };
        return RequestAsync<UpdateFuturesVxTermStructureSignalCommand,
            FuturesVxTermStructureSignalEntityId>(context, command);
    }

    /// <summary>Sends one closed observation to the event-sourced EMA actor.</summary>
    public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesEmaSignalAsync(
        this IEventActorContext context,
        FuturesTradeSessionBarReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var entityId = new FuturesTradeSessionBarEntityId(observation.MarketSeriesIdentity, observation.TimeFrame);
        GenerateFuturesEmaSignalCommand command = new()
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, GenerateFuturesEmaSignalCommand.Actor,
                GenerateFuturesEmaSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Observation = observation
        };
        return RequestAsync<GenerateFuturesEmaSignalCommand, FuturesTradeSessionBarEntityId>(context, command);
    }

    /// <summary>Sends a same-observation EMA/bar pair to the event-sourced Bollinger actor.</summary>
    public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesBbSignalAsync(
        this IEventActorContext context,
        FuturesTradeSessionBarReadModel observation,
        FuturesEmaSignalReadModel emaSignal)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(emaSignal);
        var entityId = new FuturesTradeSessionBarEntityId(observation.MarketSeriesIdentity, observation.TimeFrame);
        GenerateFuturesBbSignalCommand command = new()
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, GenerateFuturesBbSignalCommand.Actor,
                GenerateFuturesBbSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            Observation = observation,
            EmaSignal = emaSignal
        };
        return RequestAsync<GenerateFuturesBbSignalCommand, FuturesTradeSessionBarEntityId>(context, command);
    }

    /// <summary>
    /// Sends the generate futures RSI signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
      public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesRsiSignalAsync(
          this IEventActorContext context,
          FuturesRsiSignalId signalId,
          decimal futuresPrice,
          long sourceSequence = 0,
          DateTime sourceEventTimestamp = default,
          FuturesTradeSessionBarReadModel? observation = null)
    {
        var entityId = signalId.ToEntityId();
        GenerateFuturesRsiSignalCommand command = new(
            signalId,
              futuresPrice,
              sourceSequence,
              sourceEventTimestamp,
              observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesRsiSignalCommand.Actor,
                GenerateFuturesRsiSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesRsiSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(context, command);
    }

    /// <summary>
    /// Sends the generate futures TDI signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresRsiSignals">The RSI signals used to generate the result.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesTdiSignalAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesTdiSignalId signalId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        TimeFrameType timePeriod,
        FuturesTdiConfiguration? configuration = null,
        Guid? commandId = null)
        where TActor : IActor
    {
        configuration ??= FuturesTdiConfiguration.Standard;
        var normalizedSignalId = new FuturesTdiSignalId(
            signalId.ContractId,
            signalId.ValueDate,
            timePeriod,
            signalId.Timestamp,
            configuration.ConfigurationId);
        var entityId = new FuturesTdiSignalEntityId(
            signalId.ContractId,
            signalId.ValueDate,
            timePeriod,
            configuration.ConfigurationId);
        GenerateFuturesTdiSignalCommand command = new(normalizedSignalId, futuresRsiSignals, configuration)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesTdiSignalCommand.Actor,
                GenerateFuturesTdiSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesTdiSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(context, command);
    }

    /// <summary>
    /// Sends the generate futures MACD signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
      public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesMacdSignalAsync(
          this IEventActorContext context,
          FuturesMacdSignalId signalId,
          decimal futuresPrice,
          FuturesTradeSessionBarReadModel? observation = null)
    {
        var entityId = signalId.ToEntityId();
          GenerateFuturesMacdSignalCommand command = new(signalId, futuresPrice, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesMacdSignalCommand.Actor,
                GenerateFuturesMacdSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesMacdSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(context, command);
    }

    /// <summary>
    /// Sends the generate futures ADX signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesAdxSignalAsync(
        this IEventActorContext context,
        FuturesAdxSignalId signalId,
        decimal futuresPrice,
        FuturesTradeSessionBarReadModel? observation = null)
    {
        var entityId = signalId.ToEntityId();
        GenerateFuturesAdxSignalCommand command = new(signalId, futuresPrice, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesAdxSignalCommand.Actor,
                GenerateFuturesAdxSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesAdxSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(context, command);
    }

    /// <summary>
    /// Sends the generate futures ATR signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesAtrSignalAsync(
          this IEventActorContext context,
          FuturesAtrSignalId signalId,
          decimal futuresPrice,
          FuturesTradeSessionBarReadModel? observation = null)
    {
        var entityId = signalId.ToEntityId();
          GenerateFuturesAtrSignalCommand command = new(signalId, futuresPrice, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesAtrSignalCommand.Actor,
                GenerateFuturesAtrSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesAtrSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(context, command);
    }

    /// <summary>
    /// Sends the day-based Wilder ATR command for a daily, weekly, or monthly signal horizon.
    /// </summary>
    /// <param name="context">The source event actor context.</param>
    /// <param name="signalId">The day-based ATR signal identity.</param>
    /// <param name="observation">The completed daily OHLC observation.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesAtrDailySignalAsync(
        this IEventActorContext context,
        FuturesAtrSignalId signalId,
        FuturesTradeSessionBarReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var entityId = signalId.ToDailyEntityId();
        GenerateFuturesAtrDailySignalCommand command = new(signalId, observation.Close, observation)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesAtrDailySignalCommand.Actor,
                GenerateFuturesAtrDailySignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesAtrDailySignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesAtrDailySignalCommand, FuturesAtrDailySignalEntityId>(context, command);
    }

    /// <summary>
    /// Sends the update futures trade signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="futuresEodData">The futures EOD data used to update the signal.</param>
    /// <param name="futuresRsiSignal">The optional RSI signal input.</param>
    /// <param name="futuresTdiSignal">The optional TDI signal input.</param>
    /// <param name="futuresItiSignalData">The optional ITI signal input.</param>
    /// <param name="vixFuturesPrice">The current VIX futures price.</param>
    /// <param name="commandId">An optional stable command identifier used by deterministic durable derivation.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> UpdateFuturesTradeSignalAsync<TActor>(
        this IEventActorContext<TActor> context,
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesRsiSignalReadModel? futuresRsiSignal,
        FuturesTdiSignalReadModel? futuresTdiSignal,
        FuturesItiSignalDataReadModel? futuresItiSignalData,
        decimal vixFuturesPrice,
        TimeFrameType timePeriod)
        where TActor : IActor
    {
        var entityId = new FuturesTradeSignalEntityId(
            futuresEodData.ContractId ?? string.Empty,
            futuresEodData.ValueDate,
            timePeriod);
        UpdateFuturesTradeSignalCommand command = new(
            futuresEodData,
            futuresRsiSignal,
            futuresTdiSignal,
            futuresItiSignalData,
            vixFuturesPrice)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                UpdateFuturesTradeSignalCommand.Actor,
                UpdateFuturesTradeSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = UpdateFuturesTradeSignalCommand.ErrorId
        };
        return RequestAsync<UpdateFuturesTradeSignalCommand, FuturesTradeSignalEntityId>(context, command);
    }

    /// <summary>
    /// Sends the generate futures ITI signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="contractId">The contract identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <param name="timestamp">The signal timestamp.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <param name="vixFuturesPrice">The current VIX futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> GenerateFuturesItiSignalAsync(
        this IEventActorContext context,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        DateTime timestamp,
        double futuresPrice,
        double vixFuturesPrice,
        Guid? commandId = null,
        DateOnly? timeFrameStartValueDate = null)
    {
        var frameStart = timeFrameStartValueDate ?? valueDate;
        var entityId = new FuturesItiSignalEntityId(contractId, frameStart, timePeriod);
        GenerateFuturesItiSignalCommand command = new(
            contractId,
            valueDate,
            timePeriod,
            timestamp,
            futuresPrice,
            vixFuturesPrice,
            frameStart)
        {
            CommandId = commandId ?? Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesItiSignalCommand.Actor,
                GenerateFuturesItiSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesItiSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(context, command);
    }

    static async ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(IEventActorContext context, TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        var result = await context.RequestAsync<TCommand, TEntityId>(command);
        return result ?? new ServiceFailed<GuidResult>(
            command.ErrorCode,
            $"{command.CommandName} did not return a command result.");
    }
}

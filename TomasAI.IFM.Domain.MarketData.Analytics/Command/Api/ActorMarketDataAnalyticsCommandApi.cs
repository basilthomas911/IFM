using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Command.Api;

/// <summary>
/// Sends Market Data Analytics commands from a running event actor and returns their typed replies.
/// </summary>
/// <remarks>
/// Each operation constructs the command subject, entity identity, and command error code before using
/// <see cref="IEventActorContext.RequestAsync{TCommand,TEntityId}(TCommand)"/>. The instance captures one
/// actor context and must be created through <see cref="ActorMarketDataAnalyticsCommandApiFactory"/>.
/// </remarks>
public sealed class ActorMarketDataAnalyticsCommandApi(IEventActorContext context)
    : IActorMarketDataAnalyticsCommandApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    /// <summary>
    /// Sends the generate futures RSI signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> GenerateFuturesRsiSignalAsync(
        FuturesRsiSignalId signalId,
        decimal futuresPrice)
    {
        var entityId = signalId.ToEntityId();
        GenerateFuturesRsiSignalCommand command = new(signalId, futuresPrice)
        {
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesRsiSignalCommand.Actor,
                GenerateFuturesRsiSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesRsiSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(command);
    }

    /// <summary>
    /// Sends the generate futures TDI signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresRsiSignals">The RSI signals used to generate the result.</param>
    /// <param name="timePeriod">The signal time-frame type.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> GenerateFuturesTdiSignalAsync(
        FuturesTdiSignalId signalId,
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        TimeFrameType timePeriod)
    {
        var entityId = new FuturesTdiSignalEntityId(signalId.ContractId, signalId.ValueDate, timePeriod);
        GenerateFuturesTdiSignalCommand command = new(signalId, futuresRsiSignals)
        {
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesTdiSignalCommand.Actor,
                GenerateFuturesTdiSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesTdiSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesTdiSignalCommand, FuturesTdiSignalEntityId>(command);
    }

    /// <summary>
    /// Sends the generate futures MACD signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> GenerateFuturesMacdSignalAsync(
        FuturesMacdSignalId signalId,
        decimal futuresPrice)
    {
        var entityId = signalId.ToEntityId();
        GenerateFuturesMacdSignalCommand command = new(signalId, futuresPrice)
        {
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesMacdSignalCommand.Actor,
                GenerateFuturesMacdSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesMacdSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(command);
    }

    /// <summary>
    /// Sends the generate futures ADX signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> GenerateFuturesAdxSignalAsync(
        FuturesAdxSignalId signalId,
        decimal futuresPrice)
    {
        var entityId = signalId.ToEntityId();
        GenerateFuturesAdxSignalCommand command = new(signalId, futuresPrice)
        {
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesAdxSignalCommand.Actor,
                GenerateFuturesAdxSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesAdxSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(command);
    }

    /// <summary>
    /// Sends the generate futures ATR signal command and awaits its typed actor reply.
    /// </summary>
    /// <param name="signalId">The strongly typed signal identifier.</param>
    /// <param name="futuresPrice">The current futures price.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public ValueTask<ServiceResult<GuidResult>> GenerateFuturesAtrSignalAsync(
        FuturesAtrSignalId signalId,
        decimal futuresPrice)
    {
        var entityId = signalId.ToEntityId();
        GenerateFuturesAtrSignalCommand command = new(signalId, futuresPrice)
        {
            Subject = new ActorSubject(
                ActorType.Command,
                GenerateFuturesAtrSignalCommand.Actor,
                GenerateFuturesAtrSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GenerateFuturesAtrSignalCommand.ErrorId
        };
        return RequestAsync<GenerateFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(command);
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
    public ValueTask<ServiceResult<GuidResult>> UpdateFuturesTradeSignalAsync(
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesRsiSignalReadModel? futuresRsiSignal,
        FuturesTdiSignalReadModel? futuresTdiSignal,
        FuturesItiSignalDataReadModel? futuresItiSignalData,
        decimal vixFuturesPrice,
        TimeFrameType timePeriod)
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
            Subject = new ActorSubject(
                ActorType.Command,
                UpdateFuturesTradeSignalCommand.Actor,
                UpdateFuturesTradeSignalCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = UpdateFuturesTradeSignalCommand.ErrorId
        };
        return RequestAsync<UpdateFuturesTradeSignalCommand, FuturesTradeSignalEntityId>(command);
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
    public ValueTask<ServiceResult<GuidResult>> GenerateFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        DateTime timestamp,
        double futuresPrice,
        double vixFuturesPrice,
        Guid? commandId = null)
    {
        var entityId = new FuturesItiSignalEntityId(contractId, valueDate, timePeriod);
        GenerateFuturesItiSignalCommand command = new(
            contractId,
            valueDate,
            timePeriod,
            timestamp,
            futuresPrice,
            vixFuturesPrice)
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
        return RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(command);
    }

    async ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        var result = await _context.RequestAsync<TCommand, TEntityId>(command);
        if (result?.Success != true)
            throw new InvalidOperationException(result?.ErrorMessage);
        return result;
    }
}

/// <summary>
/// Creates Market Data Analytics command APIs bound to a running event actor.
/// </summary>
public sealed class ActorMarketDataAnalyticsCommandApiFactory
    : IActorMarketDataAnalyticsCommandApiFactory
{
    /// <summary>
    /// Creates a command API that dispatches through the supplied actor context.
    /// </summary>
    /// <param name="context">The actor context used for command request/reply messaging.</param>
    /// <returns>A context-bound Market Data Analytics command API.</returns>
    public IActorMarketDataAnalyticsCommandApi Create(IEventActorContext context)
        => new ActorMarketDataAnalyticsCommandApi(context);
}

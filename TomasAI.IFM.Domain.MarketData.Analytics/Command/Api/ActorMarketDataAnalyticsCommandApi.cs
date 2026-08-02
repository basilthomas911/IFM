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

public sealed class ActorMarketDataAnalyticsCommandApi(IEventActorContext context)
    : IActorMarketDataAnalyticsCommandApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

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

    public ValueTask<ServiceResult<GuidResult>> GenerateFuturesItiSignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        DateTime timestamp,
        double futuresPrice,
        double vixFuturesPrice)
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

public sealed class ActorMarketDataAnalyticsCommandApiFactory
    : IActorMarketDataAnalyticsCommandApiFactory
{
    public IActorMarketDataAnalyticsCommandApi Create(IEventActorContext context)
        => new ActorMarketDataAnalyticsCommandApi(context);
}

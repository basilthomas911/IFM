using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.Actor;

/// <summary>Acknowledges only after the incident and this Function's completion have committed.</summary>
public sealed class MarketDataFeedRecoveryRecordFunctionActor(
    IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> context,
    IEventSourceFunctionStateRepository<MarketDataFeedRecoveryRecordState, RecordMarketDataFeedRecoveryCommand> repository,
    IActorService actors,
    ILogger<MarketDataFeedRecoveryRecordFunctionActor> logger)
    : BaseEventSourceFunctionActor<MarketDataFeedRecoveryRecordFunctionActor, RecordMarketDataFeedRecoveryCommand,
        MarketDataFeedRecoveryId, MarketDataFeedRecoveryId, MarketDataFeedRecoveryRecordState,
        MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = RecordMarketDataFeedRecoveryCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, RecordMarketDataFeedRecoveryCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, RecordMarketDataFeedRecoveryCommand>>(StringComparer.Ordinal)
        {
            [RecordMarketDataFeedRecoveryCommand.Verb] = static message =>
                message.AsCommand<RecordMarketDataFeedRecoveryCommand>()! with { ReceivedAtUtc = DateTime.UtcNow }
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(RecordMarketDataFeedRecoveryCommand)] = static command =>
                ((RecordMarketDataFeedRecoveryCommand)command).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<RecordMarketDataFeedRecoveryCommand, IActorService,
        ILogger, CancellationToken, ValueTask<MarketDataFeedRecoveryRecordStoredEvent>>> _receiveMap =
        new Dictionary<Type, Func<RecordMarketDataFeedRecoveryCommand, IActorService,
            ILogger, CancellationToken, ValueTask<MarketDataFeedRecoveryRecordStoredEvent>>>
        {
            [typeof(RecordMarketDataFeedRecoveryCommand)] = static (request, service, log, token) =>
                request.ExecuteAsync(service, log, token)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<RecordMarketDataFeedRecoveryCommand, FunctionFailureStage,
        TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<RecordMarketDataFeedRecoveryCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(RecordMarketDataFeedRecoveryCommand)] = static (request, _, clock) =>
                new(clock, request.RequestDeadlineUtc, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<RecordMarketDataFeedRecoveryCommand>, TimeProvider,
        FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<RecordMarketDataFeedRecoveryCommand>, TimeProvider,
            FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>>>
        {
            [typeof(MarketDataFeedRecoveryRecordStoredEvent)] = static (input, _) =>
                FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>
                    .Complete((MarketDataFeedRecoveryRecordStoredEvent)input.Outcome!),
            [typeof(MarketDataFeedRecoveryRecordStorageFailedEvent)] = static (input, clock) =>
                FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>
                    .Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override RecordMarketDataFeedRecoveryCommand ParseMessage(
        IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> services, IActorMessage message)
    {
        var request = ParseMappedFunction(services, message, _parseMap);
        logger.LogDebug("Recovery audit record received {RecordId} {AttemptId} {RequestSubject} {ReceivedAtUtc} {RequestDeadlineUtc}",
            request.EntityId.Value, request.AttemptId, request.Subject.ToString(), request.ReceivedAtUtc, request.RequestDeadlineUtc);
        return request;
    }

    protected override ValueTask ValidateAsync(IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> services,
        ActorThreadId thread, RecordMarketDataFeedRecoveryCommand request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(RecordMarketDataFeedRecoveryCommand request,
        FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>>
        ExecuteFunctionAsync(IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> services,
            MarketDataFeedRecoveryRecordState state, RecordMarketDataFeedRecoveryCommand request, CancellationToken token)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, actors, logger, token).ConfigureAwait(false);
        return FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>
        HandleFunctionEvent(IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> services,
            FunctionEventContext<RecordMarketDataFeedRecoveryCommand> input)
        => DispatchMappedFunctionEvent(input, TimeProvider.System, _eventMap);
}

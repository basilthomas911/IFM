using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Application.Identity;
using TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function.Actor;

public sealed class StreamingRequestIdAllocationFunctionActor(
    IFunctionActorContext<StreamingRequestIdAllocationFunctionActor> context,
    IEventSourceFunctionStateRepository<StreamingRequestIdAllocationFunctionState, AllocateStreamingRequestIdCommand> repository,
    ISequenceIdApi sequences, ILogger<StreamingRequestIdAllocationFunctionActor> logger)
    : BaseEventSourceFunctionActor<StreamingRequestIdAllocationFunctionActor, AllocateStreamingRequestIdCommand, ActorEntityId, ActorEntityId,
        StreamingRequestIdAllocationFunctionState, StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = AllocateStreamingRequestIdCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, AllocateStreamingRequestIdCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, AllocateStreamingRequestIdCommand>>(StringComparer.Ordinal)
        {
            [AllocateStreamingRequestIdCommand.Verb] = message => message.AsCommand<AllocateStreamingRequestIdCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<AllocateStreamingRequestIdCommand, ISequenceIdApi, CancellationToken, ValueTask<StreamingRequestIdAllocationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<AllocateStreamingRequestIdCommand, ISequenceIdApi, CancellationToken, ValueTask<StreamingRequestIdAllocationCompletedEvent>>>
        {
            [typeof(AllocateStreamingRequestIdCommand)] = static (request, sequences, token) => request.ExecuteAsync(sequences, token)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(AllocateStreamingRequestIdCommand)] = static request => ((AllocateStreamingRequestIdCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<AllocateStreamingRequestIdCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<AllocateStreamingRequestIdCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(AllocateStreamingRequestIdCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<AllocateStreamingRequestIdCommand>, TimeProvider,
        FunctionResult<StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<AllocateStreamingRequestIdCommand>, TimeProvider,
            FunctionResult<StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent>>>
        {
            [typeof(StreamingRequestIdAllocationCompletedEvent)] = static (input, _) =>
                FunctionResult<StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent>.Complete((StreamingRequestIdAllocationCompletedEvent)input.Outcome!),
            [typeof(StreamingRequestIdAllocationFailedEvent)] = static (input, clock) =>
                FunctionResult<StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override AllocateStreamingRequestIdCommand ParseMessage(IFunctionActorContext<StreamingRequestIdAllocationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<StreamingRequestIdAllocationFunctionActor> services, ActorThreadId threadId,
        AllocateStreamingRequestIdCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(AllocateStreamingRequestIdCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<StreamingRequestIdAllocationFunctionActor> services, StreamingRequestIdAllocationFunctionState state,
        AllocateStreamingRequestIdCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, sequences, cancellationToken).ConfigureAwait(false);
        return FunctionResult<StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<StreamingRequestIdAllocationCompletedEvent, StreamingRequestIdAllocationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<StreamingRequestIdAllocationFunctionActor> services, FunctionEventContext<AllocateStreamingRequestIdCommand> input)
        => DispatchMappedFunctionEvent(input, TimeProvider.System, _eventMap);
}

using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Domain.Reference.SeedAllocation.Function.State;
using TomasAI.IFM.Domain.Reference.Shared.SeedAllocation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.SeedAllocation.Function.Actor;

public sealed class SeedAllocationFunctionActor(
    IFunctionActorContext<SeedAllocationFunctionActor> context,
    IEventSourceFunctionStateRepository<SeedAllocationFunctionState, AllocateSeedIdCommand> repository,
    ISequenceIdGenerator sequences, ILogger<SeedAllocationFunctionActor> logger)
    : BaseEventSourceFunctionActor<SeedAllocationFunctionActor, AllocateSeedIdCommand, ActorEntityId, ActorEntityId,
        SeedAllocationFunctionState, SeedAllocationCompletedEvent, SeedAllocationFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = AllocateSeedIdCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, AllocateSeedIdCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, AllocateSeedIdCommand>>(StringComparer.Ordinal)
        {
            [AllocateSeedIdCommand.Verb] = message => message.AsCommand<AllocateSeedIdCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<AllocateSeedIdCommand, ISequenceIdGenerator, CancellationToken, ValueTask<SeedAllocationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<AllocateSeedIdCommand, ISequenceIdGenerator, CancellationToken, ValueTask<SeedAllocationCompletedEvent>>>
        {
            [typeof(AllocateSeedIdCommand)] = static (request, generator, token) => request.ExecuteAsync(generator, token)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(AllocateSeedIdCommand)] = static request => ((AllocateSeedIdCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<AllocateSeedIdCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<AllocateSeedIdCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(AllocateSeedIdCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<AllocateSeedIdCommand>, TimeProvider,
        FunctionResult<SeedAllocationCompletedEvent, SeedAllocationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<AllocateSeedIdCommand>, TimeProvider,
            FunctionResult<SeedAllocationCompletedEvent, SeedAllocationFailedEvent>>>
        {
            [typeof(SeedAllocationCompletedEvent)] = static (input, _) =>
                FunctionResult<SeedAllocationCompletedEvent, SeedAllocationFailedEvent>.Complete((SeedAllocationCompletedEvent)input.Outcome!),
            [typeof(SeedAllocationFailedEvent)] = static (input, clock) =>
                FunctionResult<SeedAllocationCompletedEvent, SeedAllocationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override AllocateSeedIdCommand ParseMessage(IFunctionActorContext<SeedAllocationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<SeedAllocationFunctionActor> services, ActorThreadId threadId,
        AllocateSeedIdCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(AllocateSeedIdCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<SeedAllocationCompletedEvent, SeedAllocationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<SeedAllocationFunctionActor> services, SeedAllocationFunctionState state,
        AllocateSeedIdCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, sequences, cancellationToken).ConfigureAwait(false);
        return FunctionResult<SeedAllocationCompletedEvent, SeedAllocationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<SeedAllocationCompletedEvent, SeedAllocationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<SeedAllocationFunctionActor> services, FunctionEventContext<AllocateSeedIdCommand> input)
        => DispatchMappedFunctionEvent(input, TimeProvider.System, _eventMap);
}

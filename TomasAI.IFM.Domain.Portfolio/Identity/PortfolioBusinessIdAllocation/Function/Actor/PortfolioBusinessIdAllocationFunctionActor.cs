using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Preparation;
using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;

using TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.Actor;

public sealed class PortfolioBusinessIdAllocationFunctionActor(
    IFunctionActorContext<PortfolioBusinessIdAllocationFunctionActor> context,
    IEventSourceFunctionStateRepository<PortfolioBusinessIdAllocationFunctionState, AllocatePortfolioBusinessIdCommand> repository,
    IPortfolioBusinessIdAllocator identities, IPortfolioOperationalGuard guard, ILogger<PortfolioBusinessIdAllocationFunctionActor> logger)
    : BaseEventSourceFunctionActor<PortfolioBusinessIdAllocationFunctionActor, AllocatePortfolioBusinessIdCommand, ActorEntityId, ActorEntityId,
        PortfolioBusinessIdAllocationFunctionState, PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = AllocatePortfolioBusinessIdCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, AllocatePortfolioBusinessIdCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, AllocatePortfolioBusinessIdCommand>>(StringComparer.Ordinal)
        {
            [AllocatePortfolioBusinessIdCommand.Verb] = message => message.AsCommand<AllocatePortfolioBusinessIdCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<AllocatePortfolioBusinessIdCommand, IPortfolioBusinessIdAllocator, IPortfolioOperationalGuard, CancellationToken, ValueTask<PortfolioBusinessIdAllocationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<AllocatePortfolioBusinessIdCommand, IPortfolioBusinessIdAllocator, IPortfolioOperationalGuard, CancellationToken, ValueTask<PortfolioBusinessIdAllocationCompletedEvent>>>
        {
            [typeof(AllocatePortfolioBusinessIdCommand)] = static (request, allocator, operationalGuard, token) => request.ExecuteAsync(allocator, operationalGuard, token)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(AllocatePortfolioBusinessIdCommand)] = static request => ((AllocatePortfolioBusinessIdCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<AllocatePortfolioBusinessIdCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<AllocatePortfolioBusinessIdCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(AllocatePortfolioBusinessIdCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<AllocatePortfolioBusinessIdCommand>, TimeProvider,
        FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<AllocatePortfolioBusinessIdCommand>, TimeProvider,
            FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>>>
        {
            [typeof(PortfolioBusinessIdAllocationCompletedEvent)] = static (input, _) =>
                FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>.Complete((PortfolioBusinessIdAllocationCompletedEvent)input.Outcome!),
            [typeof(PortfolioBusinessIdAllocationFailedEvent)] = static (input, clock) =>
                FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override AllocatePortfolioBusinessIdCommand ParseMessage(IFunctionActorContext<PortfolioBusinessIdAllocationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<PortfolioBusinessIdAllocationFunctionActor> services, ActorThreadId threadId,
        AllocatePortfolioBusinessIdCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(AllocatePortfolioBusinessIdCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<PortfolioBusinessIdAllocationFunctionActor> services, PortfolioBusinessIdAllocationFunctionState state,
        AllocatePortfolioBusinessIdCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, identities, guard, cancellationToken).ConfigureAwait(false);
        return FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<PortfolioBusinessIdAllocationFunctionActor> services, FunctionEventContext<AllocatePortfolioBusinessIdCommand> input)
        => DispatchMappedFunctionEvent(input, TimeProvider.System, _eventMap);
}

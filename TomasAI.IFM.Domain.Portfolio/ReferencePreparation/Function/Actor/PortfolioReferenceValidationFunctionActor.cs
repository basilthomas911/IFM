using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Preparation;
using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;

using TomasAI.IFM.Domain.Portfolio.ReferencePreparation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.ReferencePreparation.Function.Actor;

public sealed class PortfolioReferenceValidationFunctionActor(
    IFunctionActorContext<PortfolioReferenceValidationFunctionActor> context,
    IEventSourceFunctionStateRepository<PortfolioReferenceValidationFunctionState, ValidatePortfolioReferenceCommand> repository,
    IReferenceQueryApi references, IPortfolioOperationalGuard guard, ILogger<PortfolioReferenceValidationFunctionActor> logger, TimeProvider? timeProvider = null)
    : BaseEventSourceFunctionActor<PortfolioReferenceValidationFunctionActor, ValidatePortfolioReferenceCommand, ActorEntityId, ActorEntityId,
        PortfolioReferenceValidationFunctionState, PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>(context, repository, null, logger)
{
    readonly TimeProvider businessClock = timeProvider ?? TimeProvider.System;
    public const string ActorName = ValidatePortfolioReferenceCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, ValidatePortfolioReferenceCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ValidatePortfolioReferenceCommand>>(StringComparer.Ordinal)
        {
            [ValidatePortfolioReferenceCommand.Verb] = message => message.AsCommand<ValidatePortfolioReferenceCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<ValidatePortfolioReferenceCommand, IReferenceQueryApi, IPortfolioOperationalGuard, TimeProvider, CancellationToken, ValueTask<PortfolioReferenceValidationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<ValidatePortfolioReferenceCommand, IReferenceQueryApi, IPortfolioOperationalGuard, TimeProvider, CancellationToken, ValueTask<PortfolioReferenceValidationCompletedEvent>>>
        {
            [typeof(ValidatePortfolioReferenceCommand)] = static (request, referenceApi, operationalGuard, clock, token) => request.ExecuteAsync(referenceApi, operationalGuard, token, clock)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ValidatePortfolioReferenceCommand)] = static request => ((ValidatePortfolioReferenceCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ValidatePortfolioReferenceCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ValidatePortfolioReferenceCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(ValidatePortfolioReferenceCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<ValidatePortfolioReferenceCommand>, TimeProvider,
        FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<ValidatePortfolioReferenceCommand>, TimeProvider,
            FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>>>
        {
            [typeof(PortfolioReferenceValidationCompletedEvent)] = static (input, _) =>
                FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>.Complete((PortfolioReferenceValidationCompletedEvent)input.Outcome!),
            [typeof(PortfolioReferenceValidationFailedEvent)] = static (input, clock) =>
                FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override ValidatePortfolioReferenceCommand ParseMessage(IFunctionActorContext<PortfolioReferenceValidationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<PortfolioReferenceValidationFunctionActor> services, ActorThreadId threadId,
        ValidatePortfolioReferenceCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ValidatePortfolioReferenceCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, businessClock, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<PortfolioReferenceValidationFunctionActor> services, PortfolioReferenceValidationFunctionState state,
        ValidatePortfolioReferenceCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, references, guard, businessClock, cancellationToken).ConfigureAwait(false);
        return FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<PortfolioReferenceValidationCompletedEvent, PortfolioReferenceValidationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<PortfolioReferenceValidationFunctionActor> services, FunctionEventContext<ValidatePortfolioReferenceCommand> input)
        => DispatchMappedFunctionEvent(input, businessClock, _eventMap);
}

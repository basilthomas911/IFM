using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using AuthorityPreparer = TomasAI.IFM.Domain.Portfolio.GeneralLedger.Preparation.FinancialAuthorityPreparation;
using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;

using TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialAuthorityPreparation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialAuthorityPreparation.Function.Actor;

public sealed class FinancialAuthorityPreparationFunctionActor(
    IFunctionActorContext<FinancialAuthorityPreparationFunctionActor> context,
    IEventSourceFunctionStateRepository<FinancialAuthorityPreparationFunctionState, PrepareFinancialAuthorityCommand> repository,
    AuthorityPreparer preparation, ILogger<FinancialAuthorityPreparationFunctionActor> logger)
    : BaseEventSourceFunctionActor<FinancialAuthorityPreparationFunctionActor, PrepareFinancialAuthorityCommand, ActorEntityId, ActorEntityId,
        FinancialAuthorityPreparationFunctionState, FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = PrepareFinancialAuthorityCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, PrepareFinancialAuthorityCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, PrepareFinancialAuthorityCommand>>(StringComparer.Ordinal)
        {
            [PrepareFinancialAuthorityCommand.Verb] = message => message.AsCommand<PrepareFinancialAuthorityCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<PrepareFinancialAuthorityCommand, AuthorityPreparer, CancellationToken, ValueTask<FinancialAuthorityPreparationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<PrepareFinancialAuthorityCommand, AuthorityPreparer, CancellationToken, ValueTask<FinancialAuthorityPreparationCompletedEvent>>>
        {
            [typeof(PrepareFinancialAuthorityCommand)] = static (request, preparer, token) => request.ExecuteAsync(preparer, token)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(PrepareFinancialAuthorityCommand)] = static request => ((PrepareFinancialAuthorityCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<PrepareFinancialAuthorityCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<PrepareFinancialAuthorityCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(PrepareFinancialAuthorityCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<PrepareFinancialAuthorityCommand>, TimeProvider,
        FunctionResult<FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<PrepareFinancialAuthorityCommand>, TimeProvider,
            FunctionResult<FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent>>>
        {
            [typeof(FinancialAuthorityPreparationCompletedEvent)] = static (input, _) =>
                FunctionResult<FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent>.Complete((FinancialAuthorityPreparationCompletedEvent)input.Outcome!),
            [typeof(FinancialAuthorityPreparationFailedEvent)] = static (input, clock) =>
                FunctionResult<FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override PrepareFinancialAuthorityCommand ParseMessage(IFunctionActorContext<FinancialAuthorityPreparationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<FinancialAuthorityPreparationFunctionActor> services, ActorThreadId threadId,
        PrepareFinancialAuthorityCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(PrepareFinancialAuthorityCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, TimeProvider.System, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<FinancialAuthorityPreparationFunctionActor> services, FinancialAuthorityPreparationFunctionState state,
        PrepareFinancialAuthorityCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, preparation, cancellationToken).ConfigureAwait(false);
        return FunctionResult<FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<FinancialAuthorityPreparationCompletedEvent, FinancialAuthorityPreparationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<FinancialAuthorityPreparationFunctionActor> services, FunctionEventContext<PrepareFinancialAuthorityCommand> input)
        => DispatchMappedFunctionEvent(input, TimeProvider.System, _eventMap);
}

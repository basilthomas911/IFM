using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using BookPreparer = TomasAI.IFM.Domain.Portfolio.GeneralLedger.Preparation.FinancialBookPreparation;
using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.CommandPipeline;

using TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialBookPreparation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialBookPreparation.Function.Actor;

public sealed class FinancialBookPreparationFunctionActor(
    IFunctionActorContext<FinancialBookPreparationFunctionActor> context,
    IEventSourceFunctionStateRepository<FinancialBookPreparationFunctionState, PrepareFinancialBookCommand> repository,
    BookPreparer preparation, ILogger<FinancialBookPreparationFunctionActor> logger, TimeProvider? timeProvider = null)
    : BaseEventSourceFunctionActor<FinancialBookPreparationFunctionActor, PrepareFinancialBookCommand, ActorEntityId, ActorEntityId,
        FinancialBookPreparationFunctionState, FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent>(context, repository, null, logger)
{
    readonly TimeProvider businessClock = timeProvider ?? TimeProvider.System;
    public const string ActorName = PrepareFinancialBookCommand.Actor;

    static readonly FrozenDictionary<string, Func<IActorMessage, PrepareFinancialBookCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, PrepareFinancialBookCommand>>(StringComparer.Ordinal)
        {
            [PrepareFinancialBookCommand.Verb] = message => message.AsCommand<PrepareFinancialBookCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly FrozenDictionary<Type, Func<PrepareFinancialBookCommand, BookPreparer, TimeProvider, CancellationToken, ValueTask<FinancialBookPreparationCompletedEvent>>> _receiveMap =
        new Dictionary<Type, Func<PrepareFinancialBookCommand, BookPreparer, TimeProvider, CancellationToken, ValueTask<FinancialBookPreparationCompletedEvent>>>
        {
            [typeof(PrepareFinancialBookCommand)] = static (request, preparer, clock, token) => request.ExecuteAsync(preparer, token, clock)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(PrepareFinancialBookCommand)] = static request => ((PrepareFinancialBookCommand)request).Validate()
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<PrepareFinancialBookCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<PrepareFinancialBookCommand, FunctionFailureStage, TimeProvider, FunctionExecutionPolicy>>
        {
            [typeof(PrepareFinancialBookCommand)] = static (_, _, clock) => new(clock, null, FunctionCompletionMode.EventThenProjection)
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<Type, Func<FunctionEventContext<PrepareFinancialBookCommand>, TimeProvider,
        FunctionResult<FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<PrepareFinancialBookCommand>, TimeProvider,
            FunctionResult<FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent>>>
        {
            [typeof(FinancialBookPreparationCompletedEvent)] = static (input, _) =>
                FunctionResult<FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent>.Complete((FinancialBookPreparationCompletedEvent)input.Outcome!),
            [typeof(FinancialBookPreparationFailedEvent)] = static (input, clock) =>
                FunctionResult<FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent>.Fail(input.CreateFailure(clock))
        }.ToFrozenDictionary();

    protected override PrepareFinancialBookCommand ParseMessage(IFunctionActorContext<FinancialBookPreparationFunctionActor> services, IActorMessage message)
        => ParseMappedFunction(services, message, _parseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<FinancialBookPreparationFunctionActor> services, ActorThreadId threadId,
        PrepareFinancialBookCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(PrepareFinancialBookCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, businessClock, _executionPolicyMap);

    protected override async ValueTask<FunctionResult<FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<FinancialBookPreparationFunctionActor> services, FinancialBookPreparationFunctionState state,
        PrepareFinancialBookCommand request, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedFunctionHandler(request, _receiveMap);
        var completed = await receive(request, preparation, businessClock, cancellationToken).ConfigureAwait(false);
        return FunctionResult<FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent>.Complete(completed);
    }

    protected override FunctionResult<FinancialBookPreparationCompletedEvent, FinancialBookPreparationFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<FinancialBookPreparationFunctionActor> services, FunctionEventContext<PrepareFinancialBookCommand> input)
        => DispatchMappedFunctionEvent(input, businessClock, _eventMap);
}

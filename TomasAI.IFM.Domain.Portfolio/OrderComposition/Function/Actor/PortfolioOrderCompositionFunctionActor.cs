using System.Collections.Frozen;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;

public sealed class PortfolioOrderCompositionFunctionActor(IPortfolioOrderCompositionFunctionContext services)
    : BaseEventSourceFunctionActor<PortfolioOrderCompositionFunctionActor, EvaluatePortfolioOrderCompositionCommand,
        FinancialExecutionId, FinancialExecutionId, PortfolioOrderCompositionFunctionState,
        PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>(services, services.StateRepository, null, services.Logger)
{
    public const string ActorName = EvaluatePortfolioOrderCompositionCommand.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, EvaluatePortfolioOrderCompositionCommand>> parseMap =
        new Dictionary<string, Func<IActorMessage, EvaluatePortfolioOrderCompositionCommand>>(StringComparer.Ordinal)
        { [EvaluatePortfolioOrderCompositionCommand.Verb] = message => message.AsCommand<EvaluatePortfolioOrderCompositionCommand>()! }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(EvaluatePortfolioOrderCompositionCommand)] = command =>
            new List<ValidationError>().ValidateFinancialRequest<EvaluatePortfolioOrderCompositionCommand, PortfolioOrderCandidate, List<ValidationError>>(
                (EvaluatePortfolioOrderCompositionCommand)command, ActorType.Function, ActorName, EvaluatePortfolioOrderCompositionCommand.Verb)
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<EvaluatePortfolioOrderCompositionCommand, IPortfolioOrderCompositionFunctionContext,
        Func<FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>, FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>,
        CancellationToken, ValueTask<FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>>> receiveMap =
        new Dictionary<Type, Func<EvaluatePortfolioOrderCompositionCommand, IPortfolioOrderCompositionFunctionContext,
            Func<FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>, FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>,
            CancellationToken, ValueTask<FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>>>
        { [typeof(EvaluatePortfolioOrderCompositionCommand)] = (request, owner, dispatch, token) => request.ExecuteAsync(owner, dispatch, token) }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>, TimeProvider,
        FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>> eventMap =
        new Dictionary<Type, Func<FunctionEventContext<EvaluatePortfolioOrderCompositionCommand>, TimeProvider, FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>>
        { [typeof(PortfolioOrderCompositionCompletedEvent)] = (input, clock) => input.Complete(clock), [typeof(PortfolioOrderCompositionFailedEvent)] = (input, clock) => input.Fail(clock) }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<EvaluatePortfolioOrderCompositionCommand, FunctionFailureStage, IPortfolioOrderCompositionFunctionContext, FunctionExecutionPolicy>> policyMap =
        new Dictionary<Type, Func<EvaluatePortfolioOrderCompositionCommand, FunctionFailureStage, IPortfolioOrderCompositionFunctionContext, FunctionExecutionPolicy>>
        { [typeof(EvaluatePortfolioOrderCompositionCommand)] = (request, stage, owner) => request.ResolveExecutionPolicy(stage, owner) }.ToFrozenDictionary();

    protected override EvaluatePortfolioOrderCompositionCommand ParseMessage(IFunctionActorContext<PortfolioOrderCompositionFunctionActor> context, IActorMessage message) => ParseMappedFunction(context, message, parseMap);
    protected override ValueTask ValidateAsync(IFunctionActorContext<PortfolioOrderCompositionFunctionActor> context, ActorThreadId threadId, EvaluatePortfolioOrderCompositionCommand request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); ValidateMappedCommand(request, validationMap); return ValueTask.CompletedTask; }
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(EvaluatePortfolioOrderCompositionCommand request, FunctionFailureStage stage) => DispatchMappedExecutionPolicy(request, stage, services, policyMap);
    protected override ValueTask<FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>> ExecuteFunctionAsync(IFunctionActorContext<PortfolioOrderCompositionFunctionActor> context, PortfolioOrderCompositionFunctionState state, EvaluatePortfolioOrderCompositionCommand request, CancellationToken cancellationToken) => ResolveMappedFunctionHandler(request, receiveMap)(request, services, input => HandleFunctionEvent(context, input), cancellationToken);
    protected override FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent> HandleFunctionEvent(IFunctionActorContext<PortfolioOrderCompositionFunctionActor> context, FunctionEventContext<EvaluatePortfolioOrderCompositionCommand> input) => MapEvent(input, services.TimeProvider);
    internal static FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent> MapEvent(
        FunctionEventContext<EvaluatePortfolioOrderCompositionCommand> input, TimeProvider clock)
        => DispatchMappedFunctionEvent(input, clock, eventMap);
}

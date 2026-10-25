using System.Collections.Frozen;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function.Actor;

public sealed class PortfolioCloseOrderCompositionFunctionActor(
    IPortfolioCloseOrderCompositionFunctionContext services) :
    BaseEventSourceFunctionActor<PortfolioCloseOrderCompositionFunctionActor,
        EvaluatePortfolioCloseOrderCompositionCommand, FinancialExecutionId, FinancialExecutionId,
        PortfolioCloseOrderCompositionFunctionState, PortfolioCloseOrderCompositionCompletedEvent,
        PortfolioCloseOrderCompositionFailedEvent>(
            services, services.StateRepository, null, services.Logger)
{
    public const string ActorName = EvaluatePortfolioCloseOrderCompositionCommand.Actor;

    static readonly IReadOnlyDictionary<string,
        Func<IActorMessage, EvaluatePortfolioCloseOrderCompositionCommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, EvaluatePortfolioCloseOrderCompositionCommand>>(
            StringComparer.Ordinal)
        {
            [EvaluatePortfolioCloseOrderCompositionCommand.Verb] = static message =>
                message.AsCommand<EvaluatePortfolioCloseOrderCompositionCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(EvaluatePortfolioCloseOrderCompositionCommand)] = static command =>
                new List<ValidationError>().ValidateFinancialRequest<
                    EvaluatePortfolioCloseOrderCompositionCommand, PortfolioCloseOrderCandidate, List<ValidationError>>(
                        (EvaluatePortfolioCloseOrderCompositionCommand)command,
                        ActorType.Function, ActorName,
                        EvaluatePortfolioCloseOrderCompositionCommand.Verb)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<EvaluatePortfolioCloseOrderCompositionCommand,
        IPortfolioCloseOrderCompositionFunctionContext,
        Func<FunctionEventContext<EvaluatePortfolioCloseOrderCompositionCommand>,
            FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
                PortfolioCloseOrderCompositionFailedEvent>>, CancellationToken,
        ValueTask<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
            PortfolioCloseOrderCompositionFailedEvent>>>> ReceiveMap =
        new Dictionary<Type, Func<EvaluatePortfolioCloseOrderCompositionCommand,
            IPortfolioCloseOrderCompositionFunctionContext,
            Func<FunctionEventContext<EvaluatePortfolioCloseOrderCompositionCommand>,
                FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
                    PortfolioCloseOrderCompositionFailedEvent>>, CancellationToken,
            ValueTask<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
                PortfolioCloseOrderCompositionFailedEvent>>>>
        {
            [typeof(EvaluatePortfolioCloseOrderCompositionCommand)] = static (request, owner, dispatch, token) =>
                request.ExecuteAsync(owner, dispatch, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<
        EvaluatePortfolioCloseOrderCompositionCommand>, TimeProvider,
        FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
            PortfolioCloseOrderCompositionFailedEvent>>> EventMap =
        new Dictionary<Type, Func<FunctionEventContext<EvaluatePortfolioCloseOrderCompositionCommand>,
            TimeProvider, FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
                PortfolioCloseOrderCompositionFailedEvent>>>
        {
            [typeof(PortfolioCloseOrderCompositionCompletedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(PortfolioCloseOrderCompositionFailedEvent)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<EvaluatePortfolioCloseOrderCompositionCommand,
        FunctionFailureStage, IPortfolioCloseOrderCompositionFunctionContext,
        FunctionExecutionPolicy>> PolicyMap =
        new Dictionary<Type, Func<EvaluatePortfolioCloseOrderCompositionCommand,
            FunctionFailureStage, IPortfolioCloseOrderCompositionFunctionContext,
            FunctionExecutionPolicy>>
        {
            [typeof(EvaluatePortfolioCloseOrderCompositionCommand)] = static (request, stage, owner) =>
                request.ResolveExecutionPolicy(stage, owner)
        }.ToFrozenDictionary();

    protected override EvaluatePortfolioCloseOrderCompositionCommand ParseMessage(
        IFunctionActorContext<PortfolioCloseOrderCompositionFunctionActor> context,
        IActorMessage message) => ParseMappedFunction(context, message, ParseMap);

    protected override ValueTask ValidateAsync(
        IFunctionActorContext<PortfolioCloseOrderCompositionFunctionActor> context,
        ActorThreadId threadId,
        EvaluatePortfolioCloseOrderCompositionCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(
        EvaluatePortfolioCloseOrderCompositionCommand request, FunctionFailureStage stage) =>
        DispatchMappedExecutionPolicy(request, stage, services, PolicyMap);

    protected override ValueTask<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
        PortfolioCloseOrderCompositionFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<PortfolioCloseOrderCompositionFunctionActor> context,
        PortfolioCloseOrderCompositionFunctionState state,
        EvaluatePortfolioCloseOrderCompositionCommand request,
        CancellationToken cancellationToken) =>
        ResolveMappedFunctionHandler(request, ReceiveMap)(request, services,
            input => HandleFunctionEvent(context, input), cancellationToken);

    protected override FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
        PortfolioCloseOrderCompositionFailedEvent> HandleFunctionEvent(
        IFunctionActorContext<PortfolioCloseOrderCompositionFunctionActor> context,
        FunctionEventContext<EvaluatePortfolioCloseOrderCompositionCommand> input) =>
        DispatchMappedFunctionEvent(input, services.TimeProvider, EventMap);
}

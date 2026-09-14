using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.Actor;

public sealed class FuturesTradePlanFunctionActor(IFuturesTradePlanFunctionContext actorContext)
    : BaseEventSourceFunctionActor<FuturesTradePlanFunctionActor, UpdateFuturesTradePlanCommand,
        FuturesTradePlanId, FuturesTradePlanId, FuturesTradePlanFunctionState,
        FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>(
            actorContext, actorContext.StateRepository, null, actorContext.Logger)
{
    public const string ActorName = UpdateFuturesTradePlanCommand.Actor;
    readonly IFuturesTradePlanFunctionContext _context = actorContext;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, UpdateFuturesTradePlanCommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, UpdateFuturesTradePlanCommand>>(StringComparer.Ordinal)
        {
            [UpdateFuturesTradePlanCommand.Verb] = static message => message.AsCommand<UpdateFuturesTradePlanCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(UpdateFuturesTradePlanCommand)] = static command =>
                new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
                    .CaptureCommandValidation(() => Validate((UpdateFuturesTradePlanCommand)command))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<UpdateFuturesTradePlanCommand,
        FuturesTradePlanFunctionState, IFuturesTradePlanFunctionContext,
        Func<FunctionEventContext<UpdateFuturesTradePlanCommand>, FunctionResult<FuturesTradePlanUpdatedEvent,
            TradePlanFailedEvent<FuturesTradePlanId>>>, CancellationToken,
        ValueTask<FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>>> ReceiveMap =
        new Dictionary<Type, Func<UpdateFuturesTradePlanCommand, FuturesTradePlanFunctionState,
            IFuturesTradePlanFunctionContext, Func<FunctionEventContext<UpdateFuturesTradePlanCommand>,
                FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>,
            CancellationToken, ValueTask<FunctionResult<FuturesTradePlanUpdatedEvent,
                TradePlanFailedEvent<FuturesTradePlanId>>>>>
        {
            [typeof(UpdateFuturesTradePlanCommand)] = static (request, state, context, dispatch, token) =>
                request.ExecuteAsync(state, context, dispatch, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<UpdateFuturesTradePlanCommand>,
        TimeProvider, FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>> EventMap =
        new Dictionary<Type, Func<FunctionEventContext<UpdateFuturesTradePlanCommand>, TimeProvider,
            FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>>
        {
            [typeof(FuturesTradePlanUpdatedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(TradePlanFailedEvent<FuturesTradePlanId>)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<UpdateFuturesTradePlanCommand,
        FunctionFailureStage, IFuturesTradePlanFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<UpdateFuturesTradePlanCommand, FunctionFailureStage,
            IFuturesTradePlanFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(UpdateFuturesTradePlanCommand)] = static (request, stage, context) =>
                request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(UpdateFuturesTradePlanCommand request,
        FunctionFailureStage stage) =>
        DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override UpdateFuturesTradePlanCommand ParseMessage(
        IFunctionActorContext<FuturesTradePlanFunctionActor> context, IActorMessage message) =>
        ParseMappedFunction(context, message, ParseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<FuturesTradePlanFunctionActor> context,
        ActorThreadId threadId, UpdateFuturesTradePlanCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<FunctionResult<FuturesTradePlanUpdatedEvent,
        TradePlanFailedEvent<FuturesTradePlanId>>> ExecuteFunctionAsync(
        IFunctionActorContext<FuturesTradePlanFunctionActor> context,
        FuturesTradePlanFunctionState state, UpdateFuturesTradePlanCommand request,
        CancellationToken cancellationToken) =>
        ResolveMappedFunctionHandler(request, ReceiveMap)(request, state, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    protected override FunctionResult<FuturesTradePlanUpdatedEvent,
        TradePlanFailedEvent<FuturesTradePlanId>> HandleFunctionEvent(
        IFunctionActorContext<FuturesTradePlanFunctionActor> context,
        FunctionEventContext<UpdateFuturesTradePlanCommand> input) =>
        DispatchMappedFunctionEvent(input, _context.TimeProvider, EventMap);

    static void Validate(UpdateFuturesTradePlanCommand command)
    {
        if (!command.EntityId.IsValid || command.Position.Id != command.EntityId.Position ||
            command.Position.StrategyKind != TradeStrategyKind.FuturesOutright || command.SourceEventId == Guid.Empty ||
            command.RequestedAtUtc.Kind != DateTimeKind.Utc ||
            command.Subject != new ActorSubject(ActorType.Function, ActorName,
                UpdateFuturesTradePlanCommand.Verb, command.EntityId.Format()))
            throw new ArgumentException("Valid Futures Trade Plan routing and source position are required.");
        command.Parameters.Validate();
    }
}

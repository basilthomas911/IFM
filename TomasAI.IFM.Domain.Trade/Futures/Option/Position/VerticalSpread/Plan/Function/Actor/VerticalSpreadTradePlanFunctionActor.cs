using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.Actor;

public sealed class VerticalSpreadTradePlanFunctionActor(IVerticalSpreadTradePlanFunctionContext actorContext)
    : BaseEventSourceFunctionActor<VerticalSpreadTradePlanFunctionActor, UpdateVerticalSpreadTradePlanCommand,
        VerticalSpreadTradePlanId, VerticalSpreadTradePlanId, VerticalSpreadTradePlanFunctionState,
        VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>(
            actorContext, actorContext.StateRepository, null, actorContext.Logger)
{
    public const string ActorName = UpdateVerticalSpreadTradePlanCommand.Actor;
    readonly IVerticalSpreadTradePlanFunctionContext _context = actorContext;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, UpdateVerticalSpreadTradePlanCommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, UpdateVerticalSpreadTradePlanCommand>>(StringComparer.Ordinal)
        {
            [UpdateVerticalSpreadTradePlanCommand.Verb] = static message => message.AsCommand<UpdateVerticalSpreadTradePlanCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(UpdateVerticalSpreadTradePlanCommand)] = static command =>
                new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
                    .CaptureCommandValidation(() => Validate((UpdateVerticalSpreadTradePlanCommand)command))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<UpdateVerticalSpreadTradePlanCommand,
        VerticalSpreadTradePlanFunctionState, IVerticalSpreadTradePlanFunctionContext,
        Func<FunctionEventContext<UpdateVerticalSpreadTradePlanCommand>, FunctionResult<VerticalSpreadTradePlanUpdatedEvent,
            TradePlanFailedEvent<VerticalSpreadTradePlanId>>>, CancellationToken,
        ValueTask<FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>>> ReceiveMap =
        new Dictionary<Type, Func<UpdateVerticalSpreadTradePlanCommand, VerticalSpreadTradePlanFunctionState,
            IVerticalSpreadTradePlanFunctionContext, Func<FunctionEventContext<UpdateVerticalSpreadTradePlanCommand>,
                FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>,
            CancellationToken, ValueTask<FunctionResult<VerticalSpreadTradePlanUpdatedEvent,
                TradePlanFailedEvent<VerticalSpreadTradePlanId>>>>>
        {
            [typeof(UpdateVerticalSpreadTradePlanCommand)] = static (request, state, context, dispatch, token) =>
                request.ExecuteAsync(state, context, dispatch, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<UpdateVerticalSpreadTradePlanCommand>,
        TimeProvider, FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>> EventMap =
        new Dictionary<Type, Func<FunctionEventContext<UpdateVerticalSpreadTradePlanCommand>, TimeProvider,
            FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>>
        {
            [typeof(VerticalSpreadTradePlanUpdatedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(TradePlanFailedEvent<VerticalSpreadTradePlanId>)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<UpdateVerticalSpreadTradePlanCommand,
        FunctionFailureStage, IVerticalSpreadTradePlanFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<UpdateVerticalSpreadTradePlanCommand, FunctionFailureStage,
            IVerticalSpreadTradePlanFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(UpdateVerticalSpreadTradePlanCommand)] = static (request, stage, context) =>
                request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(UpdateVerticalSpreadTradePlanCommand request,
        FunctionFailureStage stage) =>
        DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override UpdateVerticalSpreadTradePlanCommand ParseMessage(
        IFunctionActorContext<VerticalSpreadTradePlanFunctionActor> context, IActorMessage message) =>
        ParseMappedFunction(context, message, ParseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<VerticalSpreadTradePlanFunctionActor> context,
        ActorThreadId threadId, UpdateVerticalSpreadTradePlanCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<FunctionResult<VerticalSpreadTradePlanUpdatedEvent,
        TradePlanFailedEvent<VerticalSpreadTradePlanId>>> ExecuteFunctionAsync(
        IFunctionActorContext<VerticalSpreadTradePlanFunctionActor> context,
        VerticalSpreadTradePlanFunctionState state, UpdateVerticalSpreadTradePlanCommand request,
        CancellationToken cancellationToken) =>
        ResolveMappedFunctionHandler(request, ReceiveMap)(request, state, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    protected override FunctionResult<VerticalSpreadTradePlanUpdatedEvent,
        TradePlanFailedEvent<VerticalSpreadTradePlanId>> HandleFunctionEvent(
        IFunctionActorContext<VerticalSpreadTradePlanFunctionActor> context,
        FunctionEventContext<UpdateVerticalSpreadTradePlanCommand> input) =>
        DispatchMappedFunctionEvent(input, _context.TimeProvider, EventMap);

    static void Validate(UpdateVerticalSpreadTradePlanCommand command)
    {
        if (!command.EntityId.IsValid || command.Position.Id != command.EntityId.Position ||
            command.Position.StrategyKind != TradeStrategyKind.VerticalSpread || command.SourceEventId == Guid.Empty ||
            command.RequestedAtUtc.Kind != DateTimeKind.Utc ||
            command.Subject != new ActorSubject(ActorType.Function, ActorName,
                UpdateVerticalSpreadTradePlanCommand.Verb, command.EntityId.Format()))
            throw new ArgumentException("Valid Vertical Spread Trade Plan routing and source position are required.");
        command.Parameters.Validate();
    }
}

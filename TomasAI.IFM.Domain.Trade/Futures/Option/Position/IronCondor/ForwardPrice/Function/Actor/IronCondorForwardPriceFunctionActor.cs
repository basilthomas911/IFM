using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
using IronCondorPlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.Actor;

public sealed class IronCondorForwardPriceFunctionActor(IIronCondorForwardPriceFunctionContext actorContext)
    : BaseEventSourceFunctionActor<IronCondorForwardPriceFunctionActor, CalculateIronCondorForwardPriceCommand,
        IronCondorPlanId, IronCondorPlanId, IronCondorForwardPriceFunctionState,
        IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorPlanId>>(
            actorContext, actorContext.StateRepository, null, actorContext.Logger)
{
    public const string ActorName = CalculateIronCondorForwardPriceCommand.Actor;
    readonly IIronCondorForwardPriceFunctionContext _context = actorContext;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, CalculateIronCondorForwardPriceCommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, CalculateIronCondorForwardPriceCommand>>(StringComparer.Ordinal)
        {
            [CalculateIronCondorForwardPriceCommand.Verb] = static message => message.AsCommand<CalculateIronCondorForwardPriceCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(CalculateIronCondorForwardPriceCommand)] = static command =>
                new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
                    .CaptureCommandValidation(() => Validate((CalculateIronCondorForwardPriceCommand)command))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<CalculateIronCondorForwardPriceCommand,
        IronCondorForwardPriceFunctionState, IIronCondorForwardPriceFunctionContext,
        Func<FunctionEventContext<CalculateIronCondorForwardPriceCommand>, FunctionResult<IronCondorForwardPriceCalculatedEvent,
            TradePlanFailedEvent<IronCondorPlanId>>>, CancellationToken,
        ValueTask<FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>>> ReceiveMap =
        new Dictionary<Type, Func<CalculateIronCondorForwardPriceCommand, IronCondorForwardPriceFunctionState,
            IIronCondorForwardPriceFunctionContext, Func<FunctionEventContext<CalculateIronCondorForwardPriceCommand>,
                FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>,
            CancellationToken, ValueTask<FunctionResult<IronCondorForwardPriceCalculatedEvent,
                TradePlanFailedEvent<IronCondorPlanId>>>>>
        {
            [typeof(CalculateIronCondorForwardPriceCommand)] = static (request, state, context, dispatch, token) =>
                request.ExecuteAsync(state, context, dispatch, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<CalculateIronCondorForwardPriceCommand>,
        TimeProvider, FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>> EventMap =
        new Dictionary<Type, Func<FunctionEventContext<CalculateIronCondorForwardPriceCommand>, TimeProvider,
            FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>>
        {
            [typeof(IronCondorForwardPriceCalculatedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(TradePlanFailedEvent<IronCondorPlanId>)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<CalculateIronCondorForwardPriceCommand,
        FunctionFailureStage, IIronCondorForwardPriceFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<CalculateIronCondorForwardPriceCommand, FunctionFailureStage,
            IIronCondorForwardPriceFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(CalculateIronCondorForwardPriceCommand)] = static (request, stage, context) =>
                request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(CalculateIronCondorForwardPriceCommand request,
        FunctionFailureStage stage) =>
        DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override CalculateIronCondorForwardPriceCommand ParseMessage(
        IFunctionActorContext<IronCondorForwardPriceFunctionActor> context, IActorMessage message) =>
        ParseMappedFunction(context, message, ParseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<IronCondorForwardPriceFunctionActor> context,
        ActorThreadId threadId, CalculateIronCondorForwardPriceCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<FunctionResult<IronCondorForwardPriceCalculatedEvent,
        TradePlanFailedEvent<IronCondorPlanId>>> ExecuteFunctionAsync(
        IFunctionActorContext<IronCondorForwardPriceFunctionActor> context,
        IronCondorForwardPriceFunctionState state, CalculateIronCondorForwardPriceCommand request,
        CancellationToken cancellationToken) =>
        ResolveMappedFunctionHandler(request, ReceiveMap)(request, state, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    protected override FunctionResult<IronCondorForwardPriceCalculatedEvent,
        TradePlanFailedEvent<IronCondorPlanId>> HandleFunctionEvent(
        IFunctionActorContext<IronCondorForwardPriceFunctionActor> context,
        FunctionEventContext<CalculateIronCondorForwardPriceCommand> input) =>
        DispatchMappedFunctionEvent(input, _context.TimeProvider, EventMap);

    static void Validate(CalculateIronCondorForwardPriceCommand command)
    {
        if (!command.EntityId.IsValid || command.Position.Id != command.EntityId.Position ||
            command.Position.StrategyKind != TomasAI.IFM.Domain.Trade.Shared.TradeStrategyKind.IronCondor || command.SourceEventId == Guid.Empty ||
            command.RequestedAtUtc.Kind != DateTimeKind.Utc ||
            command.Subject != new ActorSubject(ActorType.Function, ActorName,
                CalculateIronCondorForwardPriceCommand.Verb, command.EntityId.Format()))
            throw new ArgumentException("Valid Iron Condor Trade Plan routing and source position are required.",nameof(command));
        ArgumentNullException.ThrowIfNull(command.IronCondorForwardPriceInputs);
    }
}

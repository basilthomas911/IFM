using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using IronCondorPlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;

public sealed class IronCondorTradePlanFunctionActor(IIronCondorTradePlanFunctionContext actorContext)
    : BaseEventSourceFunctionActor<IronCondorTradePlanFunctionActor, UpdateIronCondorTradePlanCommand,
        IronCondorPlanId, IronCondorPlanId, IronCondorTradePlanFunctionState,
        IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorPlanId>>(
            actorContext, actorContext.StateRepository, null, actorContext.Logger)
{
    public const string ActorName = UpdateIronCondorTradePlanCommand.Actor;
    readonly IIronCondorTradePlanFunctionContext _context = actorContext;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, UpdateIronCondorTradePlanCommand>> ParseMap =
        new Dictionary<string, Func<IActorMessage, UpdateIronCondorTradePlanCommand>>(StringComparer.Ordinal)
        {
            [UpdateIronCondorTradePlanCommand.Verb] = static message => message.AsCommand<UpdateIronCondorTradePlanCommand>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> ValidationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(UpdateIronCondorTradePlanCommand)] = static command =>
                new List<ValidationError>().ValidateCommandId(command.CommandId, command.CommandName)
                    .CaptureCommandValidation(() => Validate((UpdateIronCondorTradePlanCommand)command))
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<UpdateIronCondorTradePlanCommand,
        IronCondorTradePlanFunctionState, IIronCondorTradePlanFunctionContext,
        Func<FunctionEventContext<UpdateIronCondorTradePlanCommand>, FunctionResult<IronCondorTradePlanUpdatedEvent,
            TradePlanFailedEvent<IronCondorPlanId>>>, CancellationToken,
        ValueTask<FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>>> ReceiveMap =
        new Dictionary<Type, Func<UpdateIronCondorTradePlanCommand, IronCondorTradePlanFunctionState,
            IIronCondorTradePlanFunctionContext, Func<FunctionEventContext<UpdateIronCondorTradePlanCommand>,
                FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>,
            CancellationToken, ValueTask<FunctionResult<IronCondorTradePlanUpdatedEvent,
                TradePlanFailedEvent<IronCondorPlanId>>>>>
        {
            [typeof(UpdateIronCondorTradePlanCommand)] = static (request, state, context, dispatch, token) =>
                request.ExecuteAsync(state, context, dispatch, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<UpdateIronCondorTradePlanCommand>,
        TimeProvider, FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>> EventMap =
        new Dictionary<Type, Func<FunctionEventContext<UpdateIronCondorTradePlanCommand>, TimeProvider,
            FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorPlanId>>>>
        {
            [typeof(IronCondorTradePlanUpdatedEvent)] = static (input, clock) => input.Complete(clock),
            [typeof(TradePlanFailedEvent<IronCondorPlanId>)] = static (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, Func<UpdateIronCondorTradePlanCommand,
        FunctionFailureStage, IIronCondorTradePlanFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<UpdateIronCondorTradePlanCommand, FunctionFailureStage,
            IIronCondorTradePlanFunctionContext, FunctionExecutionPolicy>>
        {
            [typeof(UpdateIronCondorTradePlanCommand)] = static (request, stage, context) =>
                request.ResolveExecutionPolicy(stage, context)
        }.ToFrozenDictionary();

    protected override FunctionExecutionPolicy ResolveExecutionPolicy(UpdateIronCondorTradePlanCommand request,
        FunctionFailureStage stage) =>
        DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);

    protected override UpdateIronCondorTradePlanCommand ParseMessage(
        IFunctionActorContext<IronCondorTradePlanFunctionActor> context, IActorMessage message) =>
        ParseMappedFunction(context, message, ParseMap);

    protected override ValueTask ValidateAsync(IFunctionActorContext<IronCondorTradePlanFunctionActor> context,
        ActorThreadId threadId, UpdateIronCondorTradePlanCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, ValidationMap);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<FunctionResult<IronCondorTradePlanUpdatedEvent,
        TradePlanFailedEvent<IronCondorPlanId>>> ExecuteFunctionAsync(
        IFunctionActorContext<IronCondorTradePlanFunctionActor> context,
        IronCondorTradePlanFunctionState state, UpdateIronCondorTradePlanCommand request,
        CancellationToken cancellationToken) =>
        ResolveMappedFunctionHandler(request, ReceiveMap)(request, state, _context,
            input => HandleFunctionEvent(context, input), cancellationToken);

    protected override FunctionResult<IronCondorTradePlanUpdatedEvent,
        TradePlanFailedEvent<IronCondorPlanId>> HandleFunctionEvent(
        IFunctionActorContext<IronCondorTradePlanFunctionActor> context,
        FunctionEventContext<UpdateIronCondorTradePlanCommand> input) =>
        DispatchMappedFunctionEvent(input, _context.TimeProvider, EventMap);

    static void Validate(UpdateIronCondorTradePlanCommand command)
    {
        if (!command.EntityId.IsValid || command.Position.Id != command.EntityId.Position ||
            command.Position.StrategyKind != TomasAI.IFM.Domain.Trade.Shared.TradeStrategyKind.IronCondor || command.SourceEventId == Guid.Empty ||
            command.RequestedAtUtc.Kind != DateTimeKind.Utc ||
            command.Subject != new ActorSubject(ActorType.Function, ActorName,
                UpdateIronCondorTradePlanCommand.Verb, command.EntityId.Format()))
            throw new ArgumentException("Valid Iron Condor Trade Plan routing and source position are required.");
        command.Parameters.Validate();
    }
}

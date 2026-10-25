using System.Collections.Frozen;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;

/// <summary>Maps the CapacityConsumption Function lifecycle; financial decisions and persistence remain outside the actor.</summary>
public sealed class CapacityConsumptionFunctionActor(ICapacityConsumptionFunctionContext services)
    : BaseEventSourceFunctionActor<CapacityConsumptionFunctionActor, ConsumeCapacityReservationCommand, FinancialExecutionId, FinancialExecutionId, CapacityConsumptionFunctionState, CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>(
        services, services.StateRepository, null, services.Logger)
{
    public const string ActorName = ConsumeCapacityReservationCommand.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ConsumeCapacityReservationCommand>> _parseMap =
        new Dictionary<string, Func<IActorMessage, ConsumeCapacityReservationCommand>>(StringComparer.Ordinal)
        { [ConsumeCapacityReservationCommand.Verb] = message => message.AsCommand<ConsumeCapacityReservationCommand>()! }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap =
        new Dictionary<Type, Func<ICommand, List<ValidationError>>>
        {
            [typeof(ConsumeCapacityReservationCommand)] = command => new List<ValidationError>().ValidateFinancialRequest<ConsumeCapacityReservationCommand, CapacityLifecycleRequest, List<ValidationError>>(
            (ConsumeCapacityReservationCommand)command, ActorType.Function, ActorName, ConsumeCapacityReservationCommand.Verb)
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ConsumeCapacityReservationCommand, ICapacityConsumptionFunctionContext,
        Func<FunctionEventContext<ConsumeCapacityReservationCommand>, FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>>, CancellationToken, ValueTask<FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>>>> _receiveMap =
        new Dictionary<Type, Func<ConsumeCapacityReservationCommand, ICapacityConsumptionFunctionContext,
            Func<FunctionEventContext<ConsumeCapacityReservationCommand>, FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>>, CancellationToken, ValueTask<FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>>>>
        { [typeof(ConsumeCapacityReservationCommand)] = (request, owner, dispatch, token) => request.ExecuteAsync(owner, dispatch, token) }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<FunctionEventContext<ConsumeCapacityReservationCommand>, TimeProvider, FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>>> _eventMap =
        new Dictionary<Type, Func<FunctionEventContext<ConsumeCapacityReservationCommand>, TimeProvider, FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>>>
        {
            [typeof(CapacityConsumptionCompletedEvent)] = (input, _) => input.Complete(),
            [typeof(CapacityConsumptionFailedEvent)] = (input, clock) => input.Fail(clock)
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ConsumeCapacityReservationCommand, FunctionFailureStage, ICapacityConsumptionFunctionContext, FunctionExecutionPolicy>> _executionPolicyMap =
        new Dictionary<Type, Func<ConsumeCapacityReservationCommand, FunctionFailureStage, ICapacityConsumptionFunctionContext, FunctionExecutionPolicy>>
        { [typeof(ConsumeCapacityReservationCommand)] = (request, stage, owner) => request.ResolveExecutionPolicy(stage, owner) }.ToFrozenDictionary();

    protected override ConsumeCapacityReservationCommand ParseMessage(IFunctionActorContext<CapacityConsumptionFunctionActor> context, IActorMessage message)
        => ParseMappedFunction(context, message, _parseMap);
    protected override ValueTask ValidateAsync(IFunctionActorContext<CapacityConsumptionFunctionActor> context, ActorThreadId threadId, ConsumeCapacityReservationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMappedCommand(request, _validationMap);
        return ValueTask.CompletedTask;
    }
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ConsumeCapacityReservationCommand request, FunctionFailureStage stage)
        => DispatchMappedExecutionPolicy(request, stage, services, _executionPolicyMap);
    protected override ValueTask<FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<CapacityConsumptionFunctionActor> context, CapacityConsumptionFunctionState state, ConsumeCapacityReservationCommand request, CancellationToken cancellationToken)
        => ResolveMappedFunctionHandler(request, _receiveMap)(request, services, input => HandleFunctionEvent(context, input), cancellationToken);
    protected override FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent> HandleFunctionEvent(IFunctionActorContext<CapacityConsumptionFunctionActor> context, FunctionEventContext<ConsumeCapacityReservationCommand> input)
        => MapEvent(input, services.TimeProvider);
    internal static FunctionResult<CapacityConsumptionCompletedEvent, CapacityConsumptionFailedEvent> MapEvent(FunctionEventContext<ConsumeCapacityReservationCommand> input, TimeProvider clock)
        => DispatchMappedFunctionEvent(input, clock, _eventMap);
}


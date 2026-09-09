using System.Collections.Frozen;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;

/// <summary>Maps the CapacityReservation Function lifecycle; financial decisions and persistence remain outside the actor.</summary>
public sealed class CapacityReservationFunctionActor(ICapacityReservationFunctionContext context)
    : BaseEventSourceFunctionActor<CapacityReservationFunctionActor,ReservePortfolioTradeRiskCommand,FinancialExecutionId,FinancialExecutionId,CapacityReservationFunctionState,CapacityReservationCompletedEvent,CapacityReservationFailedEvent>(
        context,context.StateRepository,null,context.Logger)
{
    public const string ActorName=ReservePortfolioTradeRiskCommand.Actor;
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,ReservePortfolioTradeRiskCommand>> _parseMap=
        new Dictionary<string,Func<IActorMessage,ReservePortfolioTradeRiskCommand>>(StringComparer.Ordinal)
        { [ReservePortfolioTradeRiskCommand.Verb]=message=>message.AsCommand<ReservePortfolioTradeRiskCommand>()! }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<ICommand,List<ValidationError>>> _validationMap=
        new Dictionary<Type,Func<ICommand,List<ValidationError>>>
        { [typeof(ReservePortfolioTradeRiskCommand)]=command=>new List<ValidationError>().ValidateFinancialRequest<ReservePortfolioTradeRiskCommand,CapacityReservationRequest>(
            (ReservePortfolioTradeRiskCommand)command,ActorType.Function,ActorName,ReservePortfolioTradeRiskCommand.Verb) }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,Func<ReservePortfolioTradeRiskCommand,ICapacityReservationFunctionContext,
        Func<FunctionEventContext<ReservePortfolioTradeRiskCommand>,FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>,CancellationToken,ValueTask<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>>> _receiveMap=
        new Dictionary<Type,Func<ReservePortfolioTradeRiskCommand,ICapacityReservationFunctionContext,
            Func<FunctionEventContext<ReservePortfolioTradeRiskCommand>,FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>,CancellationToken,ValueTask<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>>>
        { [typeof(ReservePortfolioTradeRiskCommand)]=(request,owner,dispatch,token)=>request.ExecuteAsync(owner,dispatch,token) }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,Func<FunctionEventContext<ReservePortfolioTradeRiskCommand>,TimeProvider,FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>> _eventMap=
        new Dictionary<Type,Func<FunctionEventContext<ReservePortfolioTradeRiskCommand>,TimeProvider,FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>>>
        {
            [typeof(CapacityReservationCompletedEvent)]=(input,_)=>input.Complete(),
            [typeof(CapacityReservationFailedEvent)]=(input,clock)=>input.Fail(clock)
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,Func<ReservePortfolioTradeRiskCommand,FunctionFailureStage,ICapacityReservationFunctionContext,FunctionExecutionPolicy>> _executionPolicyMap=
        new Dictionary<Type,Func<ReservePortfolioTradeRiskCommand,FunctionFailureStage,ICapacityReservationFunctionContext,FunctionExecutionPolicy>>
        { [typeof(ReservePortfolioTradeRiskCommand)]=(request,stage,owner)=>request.ResolveExecutionPolicy(stage,owner) }.ToFrozenDictionary();

    protected override ReservePortfolioTradeRiskCommand ParseMessage(IFunctionActorContext<CapacityReservationFunctionActor> owner,IActorMessage message)
        =>ParseMappedFunction(owner,message,_parseMap);
    protected override ValueTask ValidateAsync(IFunctionActorContext<CapacityReservationFunctionActor> owner,ActorThreadId threadId,ReservePortfolioTradeRiskCommand request,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ValidateMappedCommand(request,_validationMap);
        return ValueTask.CompletedTask;
    }
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(ReservePortfolioTradeRiskCommand request,FunctionFailureStage stage)
        =>DispatchMappedExecutionPolicy(request,stage,context,_executionPolicyMap);
    protected override ValueTask<FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<CapacityReservationFunctionActor> owner,CapacityReservationFunctionState state,ReservePortfolioTradeRiskCommand request,CancellationToken token)
        =>ResolveMappedFunctionHandler(request,_receiveMap)(request,context,input=>HandleFunctionEvent(owner,input),token);
    protected override FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent> HandleFunctionEvent(IFunctionActorContext<CapacityReservationFunctionActor> owner,FunctionEventContext<ReservePortfolioTradeRiskCommand> input)
        =>MapEvent(input,context.TimeProvider);
    internal static FunctionResult<CapacityReservationCompletedEvent,CapacityReservationFailedEvent> MapEvent(FunctionEventContext<ReservePortfolioTradeRiskCommand> input,TimeProvider clock)
        =>DispatchMappedFunctionEvent(input,clock,_eventMap);
}


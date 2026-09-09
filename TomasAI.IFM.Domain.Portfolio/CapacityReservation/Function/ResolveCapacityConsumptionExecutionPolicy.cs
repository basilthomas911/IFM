using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function;

/// <summary>Allows bounded receipt recovery after expiry while new financial work retains the original deadline.</summary>
public static class ResolveCapacityConsumptionExecutionPolicy
{
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this ConsumeCapacityReservationCommand request,FunctionFailureStage stage,ICapacityConsumptionFunctionContext context)
    {
        var budget=request.RequestedAtUtc.AddSeconds(2);
        var deadline=stage==FunctionFailureStage.Loading?context.TimeProvider.GetUtcNow().UtcDateTime.AddSeconds(1)
            :request.ExpiresAtUtc<budget?request.ExpiresAtUtc:budget;
        return new(context.TimeProvider,deadline,FunctionCompletionMode.AtomicBusinessAndEvent);
    }
}

using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function.Actor;

public sealed class StreamingRequestIdAllocationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, StreamingRequestIdAllocationFunctionActor.ActorName)),
      IFunctionActorContext<StreamingRequestIdAllocationFunctionActor>;

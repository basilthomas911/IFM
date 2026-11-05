using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FeedSessionPreparation.Function.Actor;

public sealed class FeedSessionPreparationFunctionContext(IActorSupervisor supervisor)
    : FunctionActorContext(supervisor, new ActorMailboxId(ActorType.Function, FeedSessionPreparationFunctionActor.ActorName)),
      IFunctionActorContext<FeedSessionPreparationFunctionActor>;

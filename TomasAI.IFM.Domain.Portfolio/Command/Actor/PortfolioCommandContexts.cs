using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, PortfolioCommandActor.ActorName)),
      ICommandActorContext<PortfolioCommandActor>;

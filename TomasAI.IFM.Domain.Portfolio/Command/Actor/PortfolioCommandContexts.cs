using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.Actor;

public sealed class PortfolioCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, PortfolioCommandActor.ActorName)),
      ICommandActorContext<PortfolioCommandActor>;

public sealed class PortfolioFundCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, PortfolioFundCommandActor.ActorName)),
      ICommandActorContext<PortfolioFundCommandActor>;

public sealed class PortfolioFinancialPolicyCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, PortfolioFinancialPolicyCommandActor.ActorName)),
      ICommandActorContext<PortfolioFinancialPolicyCommandActor>;

using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command.Actor;

/// <summary>Provides the Fund command actor's mailbox and supervisor context.</summary>
public sealed class PortfolioFundCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, PortfolioFundCommandActor.ActorName)),
      ICommandActorContext<PortfolioFundCommandActor>;

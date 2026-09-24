using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Actor;

/// <summary>Provides the Financial Policy command actor's mailbox and supervisor context.</summary>
public sealed class PortfolioFinancialPolicyCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, PortfolioFinancialPolicyCommandActor.ActorName)),
      ICommandActorContext<PortfolioFinancialPolicyCommandActor>;

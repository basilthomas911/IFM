using TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Validation;
using TomasAI.IFM.Domain.Reference.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Actor;

/// <summary>Provides the Trade Strategy Family command mailbox context.</summary>
public sealed class TradeStrategyFamilyCommandContext(IActorSupervisor supervisor, ILogger<TradeStrategyFamilyCommandActor> logger)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, TradeStrategyFamilyCommandActor.ActorName)), ICommandActorContext<TradeStrategyFamilyCommandActor>
{
    /// <summary>Gets the actor logger.</summary>
    public ILogger<TradeStrategyFamilyCommandActor> Logger { get; } = logger;
}

using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;

public sealed class LedgerConfigurationCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, LedgerConfigurationCommandActor.ActorName)), ICommandActorContext<LedgerConfigurationCommandActor>;



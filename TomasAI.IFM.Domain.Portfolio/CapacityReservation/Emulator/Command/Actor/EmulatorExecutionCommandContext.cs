using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command.Actor;

public sealed class EmulatorExecutionCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, EmulatorExecutionCommandActor.ActorName)), ICommandActorContext<EmulatorExecutionCommandActor>;


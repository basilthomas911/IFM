using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command;

public sealed record EmulatorExecutionCommandServices(EmulatorExecutionStore Store, IPortfolioDbReadContext Database,
    IEventProjector<EmulatorExecutionCommandActor> Projector, ILogger<EmulatorExecutionCommandActor> Logger);

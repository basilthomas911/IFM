using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command;

public sealed record CapacityReservationCommandServices(ICapacityReservationStore Store, IPortfolioDbReadContext Database,
    IEventProjector<CapacityReservationCommandActor> Projector, ILogger<CapacityReservationCommandActor> Logger);

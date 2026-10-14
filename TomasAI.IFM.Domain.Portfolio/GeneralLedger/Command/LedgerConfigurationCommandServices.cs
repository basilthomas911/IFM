using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

public sealed record LedgerConfigurationCommandServices(ILedgerConfigurationStore Store,
    IPortfolioDbReadContext Database, IPortfolioEventStore Sources, IEventProjector<LedgerConfigurationCommandActor> Projector,
    ILogger<LedgerConfigurationCommandActor> Logger, FinancialDevelopmentPolicy? DevelopmentPolicy = null);

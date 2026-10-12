using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Actor;
public sealed class GeneralLedgerQueryContext(IActorSupervisor supervisor)
    : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, GeneralLedgerQueryActor.ActorName)), IQueryActorContext<GeneralLedgerQueryActor>;


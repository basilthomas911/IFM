using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Fund.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using GetFundQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundQuery;
using GetFundRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRevisionQuery;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsQuery;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundAllocationQuery;
using GetFundRiskEnvelopeQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRiskEnvelopeQuery;
using GetFundTemplateAssignmentsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundTemplateAssignmentsQuery;
using ResolveForSelectionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.ResolveForSelectionQuery;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategySnapshotQuery;
using GetFundOrderByOrderIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderByOrderIdQuery;
using GetFundOrderTradeByTradeIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradeByTradeIdQuery;
using GetFundCompositionByWorkflowQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundCompositionByWorkflowQuery;
using GetFundOrdersPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrdersPageQuery;
using GetFundOrderTradesPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradesPageQuery;
using GetPortfolioFundStrategyReferenceCombinationsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategyReferenceCombinationsQuery;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query.Actor;

/// <summary>Provides the Fund query mailbox for the split actor route.</summary>
/// <param name="supervisor">The actor supervisor.</param>
public sealed class PortfolioFundQueryContext(IActorSupervisor supervisor)
    : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, PortfolioFundQueryActor.ActorName)),
        IQueryActorContext<PortfolioFundQueryActor>;

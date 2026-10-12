using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using AllocatePortfolioBusinessIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.AllocatePortfolioBusinessIdQuery;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePortfolioFinancialPolicyQuery;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundAllocationQuery;
using GetFundCompositionByWorkflowQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundCompositionByWorkflowQuery;
using GetFundOrderByOrderIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderByOrderIdQuery;
using GetFundOrderTradeByTradeIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradeByTradeIdQuery;
using GetFundOrderTradesPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradesPageQuery;
using GetFundOrdersPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrdersPageQuery;
using GetFundQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundQuery;
using GetFundRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRevisionQuery;
using GetFundRiskEnvelopeQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRiskEnvelopeQuery;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsQuery;
using GetFundTemplateAssignmentsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundTemplateAssignmentsQuery;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPoliciesQuery;
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPolicyQuery;
using GetPortfolioFundStrategyReferenceCombinationsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategyReferenceCombinationsQuery;
using ResolveForSelectionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.ResolveForSelectionQuery;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategySnapshotQuery;
using GetPortfolioQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioQuery;
using GetPortfolioRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioRevisionQuery;
using GetPortfoliosQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfoliosQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query.Actor;

public interface IPortfolioQueryContext : IQueryActorContext<PortfolioQueryActor>
{
    IDbContextFactory DbFactory { get; }
    IPortfolioBusinessIdAllocator IdentityAllocator { get; }
    ILogger<PortfolioQueryActor> Logger { get; }
}

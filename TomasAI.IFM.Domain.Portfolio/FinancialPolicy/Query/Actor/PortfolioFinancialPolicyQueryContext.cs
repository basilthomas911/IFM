using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query;
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
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPolicyQuery;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPoliciesQuery;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePortfolioFinancialPolicyQuery;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query.Actor;

/// <summary>Provides the Financial Policy query mailbox for the split actor route.</summary>
/// <param name="supervisor">The actor supervisor.</param>
public sealed class PortfolioFinancialPolicyQueryContext(IActorSupervisor supervisor)
    : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, PortfolioFinancialPolicyQueryActor.ActorName)),
        IQueryActorContext<PortfolioFinancialPolicyQueryActor>;

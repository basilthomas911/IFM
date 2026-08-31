using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Command.Model;

public abstract record PortfolioFinancialPolicyDomainEvent(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal)
    : PortfolioDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record PortfolioFinancialPolicyCreated(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    PortfolioFinancialPolicyReadModel Policy, Guid IdempotencyKey)
    : PortfolioFinancialPolicyDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record PortfolioFinancialPolicyVersionAdded(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    PortfolioFinancialPolicyReadModel Policy)
    : PortfolioFinancialPolicyDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record PortfolioFinancialPolicyActivated(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    long PolicyVersion)
    : PortfolioFinancialPolicyDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record PortfolioFinancialPolicyRetired(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal,
    long PolicyVersion, string Reason)
    : PortfolioFinancialPolicyDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

public sealed record DraftPortfolioFinancialPolicyDeleted(
    Guid Id, Guid CommandId, long Revision, DateTime OccurredOnUtc, string Principal, string Reason)
    : PortfolioFinancialPolicyDomainEvent(Id, CommandId, Revision, OccurredOnUtc, Principal);

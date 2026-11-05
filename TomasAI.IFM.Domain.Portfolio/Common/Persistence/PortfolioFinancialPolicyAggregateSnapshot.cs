using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

#pragma warning disable CA1819 // Checkpoint payloads use ordered arrays for deterministic serialization.
/// <summary>Everything the policy reducer needs to resume decisions after a checkpoint.</summary>
public sealed record PortfolioFinancialPolicyAggregateSnapshot(long Revision, long CurrentPolicyVersion,
    PortfolioFinancialPolicyReadModel[] Versions, bool EverActive, bool IsDeleted, Guid[] AppliedCommandIds);
#pragma warning restore CA1819

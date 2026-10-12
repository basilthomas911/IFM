using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

#pragma warning disable CA1819 // Preserve the published snapshot array serialization contract.
public sealed record PortfolioAggregateSnapshot(
    long Revision,
    PortfolioReadModel Current,
    int[] FundIds,
    FundAllocationReadModel[] Allocations,
    FundRiskEnvelopeReadModel[] RiskEnvelopes,
    Guid[] AppliedCommandIds);

#pragma warning restore CA1819

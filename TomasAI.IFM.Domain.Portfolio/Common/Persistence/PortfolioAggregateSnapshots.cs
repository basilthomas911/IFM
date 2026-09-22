using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

public sealed record PortfolioAggregateSnapshot(
    long Revision,
    PortfolioReadModel Current,
    int[] FundIds,
    FundAllocationReadModel[] Allocations,
    FundRiskEnvelopeReadModel[] RiskEnvelopes,
    Guid[] AppliedCommandIds);

public sealed record PortfolioFundAggregateSnapshot(
    long Revision,
    FundMandateReadModel Current,
    FundTradeTemplateAssignmentReadModel[] Assignments,
    FundCompositionReservationResult[] Compositions,
    Guid[] AppliedCommandIds);

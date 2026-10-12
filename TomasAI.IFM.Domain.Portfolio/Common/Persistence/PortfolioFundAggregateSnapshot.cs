using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

#pragma warning disable CA1819 // Preserve the published snapshot array serialization contract.
public sealed record PortfolioFundAggregateSnapshot(
    long Revision,
    FundMandateReadModel Current,
    FundTradeTemplateAssignmentReadModel[] Assignments,
    FundCompositionReservationResult[] Compositions,
    Guid[] AppliedCommandIds);

#pragma warning restore CA1819

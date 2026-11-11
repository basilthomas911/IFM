using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>The latest committed stream payload, including unsupported or missing tails for recovery diagnostics.</summary>
/// <param name="Stream">The authoritative stream identity used as a stable page cursor.</param>
/// <param name="Event">The last committed source event, or null for an empty stream.</param>
public sealed record FinancialWorkflowSourceRow(long Stream, EventLogReadModel? Event);

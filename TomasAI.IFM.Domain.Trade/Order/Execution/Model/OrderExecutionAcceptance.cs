using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Model;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the accepted execution fact array contract consumed by posting and position handlers.")]
public sealed record OrderExecutionAcceptance(EstablishedTradeDefinition[] CreatedTrades, PositionCloseExecution[] ClosedPositions);

using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record FuturesRolloverContractAssignment
{
    public required DatabentoContractRole ContractRole { get; init; }
    public required string RootSymbol { get; init; }
    public required string ContractId { get; init; }
    public required string Description { get; init; }
    public required string LocalSymbol { get; init; }
    public required string SecurityType { get; init; }
    public required string Currency { get; init; }
    public required string Exchange { get; init; }
    public required string Multiplier { get; init; }
    public required DateOnly LastTradeDate { get; init; }
    public required DateOnly NextRolloverDate { get; init; }
    public required string SourceContractHash { get; init; }
    public long RowVersion { get; init; } = 1;
    public required DateTime CreatedOnUtc { get; init; }
    public required string CreatedBy { get; init; }
    public required DateTime UpdatedOnUtc { get; init; }
    public required string UpdatedBy { get; init; }
}

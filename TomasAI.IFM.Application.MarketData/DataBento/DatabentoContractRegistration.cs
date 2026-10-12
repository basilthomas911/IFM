using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed record DatabentoContractRegistration
{
    public required string DomainContractId { get; init; }
    public required string ProviderContractName { get; init; }
    public required AssetTypeId AssetTypeId { get; init; }
    public string? RootSymbol { get; init; }
    public string? Dataset { get; init; }
    public bool OnTheRun { get; init; }
    public bool Rollover { get; init; }
}

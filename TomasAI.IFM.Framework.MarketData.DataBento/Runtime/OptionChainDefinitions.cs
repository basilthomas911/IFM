using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record OptionChainDefinitions
{
    public required string Dataset { get; init; }
    public required string Underlying { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public required OptionUniversePolicy UniversePolicy { get; init; }
    public required OptionRightSelection Rights { get; init; }
    public required IReadOnlyList<OptionContractDefinition> Contracts { get; init; }
}

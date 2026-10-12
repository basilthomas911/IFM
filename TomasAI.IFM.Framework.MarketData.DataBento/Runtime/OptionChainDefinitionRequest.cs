using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record OptionChainDefinitionRequest
{
    public required string Dataset { get; init; }
    public required string Underlying { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public OptionUniversePolicy UniversePolicy { get; init; } =
        OptionUniversePolicy.ParentOptionSymbol;
    public IReadOnlyList<string> ExplicitOptionRoots { get; init; } = [];
    public OptionRightSelection Rights { get; init; } = OptionRightSelection.Both;
}

using System.Collections.Frozen;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Resolves the domain descriptor for each editable parameter-set component.</summary>
public static class ParameterComponentModelRegistry
{
    static readonly System.Collections.Frozen.FrozenDictionary<string, IParameterComponentDescriptor> Descriptors =
        new IParameterComponentDescriptor[]
        {
            new RegimeDiscoveryParameterModel(),
            new FuturesItiSignalParameterModel(),
            new OptionVolatilitySeriesParameterModel(),
            new OptionVolatilityConsumerRulesParameterModel(),
            new OptionVolatilityRetentionParameterModel(),
            new IronCondorMarketSelectionParameterModel(),
            new StrategyOptionChainParameterModel(),
            new VerticalSpreadMarketSelectionParameterModel()
        }.ToFrozenDictionary(descriptor => descriptor.Summary.ComponentCode, StringComparer.Ordinal);

    /// <summary>Gets the registered descriptor for a component.</summary>
    public static IParameterComponentDescriptor Get(string componentCode) =>
        Descriptors.TryGetValue(componentCode, out var descriptor)
            ? descriptor
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            : throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED");
#pragma warning restore MA0015

    /// <summary>Determines whether the component has a registered editable descriptor.</summary>
    public static bool Contains(string componentCode) => Descriptors.ContainsKey(componentCode);
}

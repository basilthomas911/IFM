using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Migration;

/// <summary>Verified conversion evidence. The historical reference stays unchanged; the typed projection has its own fingerprint.</summary>
public sealed record VerifiedParameterMigration(ParameterVersionRef OriginalReference, ParameterSetValue Parameters, string TypedContentSha256);

/// <summary>Reads historical JSON facts only. New parameter commands and events must use the concrete MessagePack contract.</summary>
public static class ParameterLegacyUpcaster
{
    /// <summary>Verifies historical content, identity, schema and known fields before materializing typed projection values.</summary>
    public static VerifiedParameterMigration Verify(ParameterSetVersion historical)
    {
        ArgumentNullException.ThrowIfNull(historical);
        var reference = historical.Reference;
        if (!string.Equals(ParameterCanonicalPayloadModel.Hash(historical.PayloadJson), reference.PayloadSha256, StringComparison.Ordinal))
            throw new InvalidDataException("PARAM.LEGACY_HASH_MISMATCH");
        if (!ParameterSchemaRegistry.Default.CanEditLosslessly(reference.ComponentCode, historical.SchemaVersion, historical.PayloadJson))
            throw new InvalidDataException("PARAM.LEGACY_FIELDS_UNSUPPORTED");
        var value = Read(reference.ComponentCode, historical.PayloadJson);
        if (value.SetId != reference.SetId || value.Version != reference.Version || value.SchemaVersion != historical.SchemaVersion)
            throw new InvalidDataException("PARAM.LEGACY_IDENTITY_MISMATCH");
        return new(reference, value, ParameterValueCodec.Hash(value));
    }

    static ParameterSetValue Read(string component, string json) => component switch
    {
        ParameterSchemaRegistry.RegimeComponent => ParameterSetValue.From(Parse<RegimeDiscoveryParameterSet>(json)),
        ParameterSchemaRegistry.FuturesItiSignalComponent => ParameterSetValue.From(Parse<FuturesItiSignalParameterSet>(json)),
        ParameterSchemaRegistry.OptionVolatilitySeriesComponent => ParameterSetValue.From(Parse<OptionVolatilitySeriesParameterSet>(json)),
        ParameterSchemaRegistry.OptionVolatilityConsumerRulesComponent => ParameterSetValue.From(Parse<OptionVolatilityConsumerRulesParameterSet>(json)),
        ParameterSchemaRegistry.OptionVolatilityRetentionComponent => ParameterSetValue.From(Parse<OptionVolatilityRetentionParameterSet>(json)),
        ParameterSchemaRegistry.IronCondorMarketSelectionComponent => ParameterSetValue.From(Parse<IronCondorMarketSelectionParameterSet>(json)),
        ParameterSchemaRegistry.VerticalSpreadMarketSelectionComponent => ParameterSetValue.From(Parse<VerticalSpreadMarketSelectionParameterSet>(json)),
        ParameterSchemaRegistry.StrategyOptionChainCacheComponent => ParameterSetValue.From(Parse<StrategyOptionChainParameterSet>(json)),
        _ => throw new InvalidDataException("PARAM.LEGACY_COMPONENT_UNSUPPORTED")
    };
    static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json) ?? throw new InvalidDataException("PARAM.LEGACY_VALUE_MISSING");
}

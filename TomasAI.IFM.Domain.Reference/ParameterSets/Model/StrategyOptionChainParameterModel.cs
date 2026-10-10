using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Reuses existing parameter command/event actors for global chain policy authoring and publication.</summary>
public sealed class StrategyOptionChainParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.StrategyOptionChainCacheComponent;
    /// <inheritdoc />
    public ParameterComponentSummary Summary => new("market-data", "Market Data", ComponentCode, "Strategy Option Chain Cache", [1], true);
    /// <summary>Creates an unbound disabled draft; an exact strategy catalog version is required before publication.</summary>
    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(StrategyOptionChainParameterDefaults.IronCondor(setId, Guid.Empty, 0));
    /// <inheritdoc />
    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion)
    {
        var structural = ParameterSchemaRegistry.Default.ValidateStructure(ComponentCode, schemaVersion, payloadJson);
        if (structural.Length != 0) return structural;
        try { StrategyOptionChainParameterSet.Read(payloadJson); return []; }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        { return [new("PARAM.OPTION_CHAIN_INVALID", "Payload", ex.Message)]; }
    }
}

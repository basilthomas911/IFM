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
    public ParameterSetValue CreateTypedDraft(Guid setId)=>ParameterSetValue.From(StrategyOptionChainParameterDefaults.IronCondor(setId,Guid.Empty,0));
    public ParameterValidationIssue[] ValidateTyped(ParameterSetValue parameters,int schemaVersion)
    {
        if(parameters.ComponentCode!=ComponentCode||parameters.SchemaVersion!=schemaVersion)return [new("PARAM.SCHEMA_MISMATCH","Parameters","The typed configuration does not match the component or schema")];
        try { parameters.Get<StrategyOptionChainParameterSet>().Validate();return []; }
        catch(ArgumentException error){return [new("PARAM.OPTION_CHAIN_INVALID","Parameters",error.Message)];}
    }
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

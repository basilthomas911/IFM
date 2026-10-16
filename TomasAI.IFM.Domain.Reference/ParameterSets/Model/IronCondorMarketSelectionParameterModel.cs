using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Authors and validates per-symbol Iron Condor Market Selection defaults.</summary>
public sealed class IronCondorMarketSelectionParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.IronCondorMarketSelectionComponent;
    public ParameterComponentSummary Summary => new("option-spread-strategy", "Option Spread Strategy Defaults",
        ComponentCode, "Iron Condor", [ParameterSchemaRegistry.CurrentIronCondorMarketSelectionSchemaVersion], true);
    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateDefault(setId));
    public static IronCondorMarketSelectionParameterSet CreateDefault(Guid setId) => new()
    {
        ParameterSetId = setId,
        DefaultSymbol = "ES",
        Symbols = [new() { Symbol = "ES", ShortCallDelta = 16, CallSpreadWidth = 50,
            ShortPutDelta = 16, PutSpreadWidth = 50 }]
    };
    public ParameterSetValue CreateTypedDraft(Guid setId)=>ParameterSetValue.From(CreateDefault(setId));
    public ParameterValidationIssue[] ValidateTyped(ParameterSetValue parameters,int schemaVersion) =>
        parameters.ComponentCode != ComponentCode || parameters.SchemaVersion != schemaVersion
            ? [new("PARAM.SCHEMA_MISMATCH", "Parameters", "The typed configuration does not match the component or schema")]
            : OptionSpreadStrategyParameterValidation.Validate<IronCondorMarketSelectionParameterSet>(
            parameters.Get<IronCondorMarketSelectionParameterSet>(), schemaVersion, value =>
            {
                var issues = OptionSpreadStrategyParameterValidation.Common(value.SchemaVersion,
                    value.ParameterSetId, value.Version, value.DefaultSymbol, value.Symbols.Select(x => x.Symbol));
                foreach (var (symbol, index) in value.Symbols.Select((symbol, index) => (symbol, index)))
                {
                    OptionSpreadStrategyParameterValidation.Delta(symbol.ShortCallDelta, $"Symbols/{index}/ShortCallDelta", issues);
                    OptionSpreadStrategyParameterValidation.Width(symbol.CallSpreadWidth, $"Symbols/{index}/CallSpreadWidth", issues);
                    OptionSpreadStrategyParameterValidation.Delta(symbol.ShortPutDelta, $"Symbols/{index}/ShortPutDelta", issues);
                    OptionSpreadStrategyParameterValidation.Width(symbol.PutSpreadWidth, $"Symbols/{index}/PutSpreadWidth", issues);
                }
                return issues;
            });
    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion) =>
        OptionSpreadStrategyParameterValidation.Validate<IronCondorMarketSelectionParameterSet>(
            ComponentCode, payloadJson, schemaVersion, value =>
            {
                var issues = OptionSpreadStrategyParameterValidation.Common(value.SchemaVersion,
                    value.ParameterSetId, value.Version, value.DefaultSymbol, value.Symbols.Select(x => x.Symbol));
                foreach (var (symbol, index) in value.Symbols.Select((symbol, index) => (symbol, index)))
                {
                    OptionSpreadStrategyParameterValidation.Delta(symbol.ShortCallDelta, $"Symbols/{index}/ShortCallDelta", issues);
                    OptionSpreadStrategyParameterValidation.Width(symbol.CallSpreadWidth, $"Symbols/{index}/CallSpreadWidth", issues);
                    OptionSpreadStrategyParameterValidation.Delta(symbol.ShortPutDelta, $"Symbols/{index}/ShortPutDelta", issues);
                    OptionSpreadStrategyParameterValidation.Width(symbol.PutSpreadWidth, $"Symbols/{index}/PutSpreadWidth", issues);
                }
                return issues;
            });
}

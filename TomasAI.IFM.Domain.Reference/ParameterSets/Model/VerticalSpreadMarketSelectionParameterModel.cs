using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Authors and validates per-symbol Vertical Spread Market Selection defaults.</summary>
public sealed class VerticalSpreadMarketSelectionParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.VerticalSpreadMarketSelectionComponent;
    public ParameterComponentSummary Summary => new("option-spread-strategy", "Option Spread Strategy Defaults",
        ComponentCode, "Vertical Spreads", [ParameterSchemaRegistry.CurrentVerticalSpreadMarketSelectionSchemaVersion], true);
    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateDefault(setId));
    public static VerticalSpreadMarketSelectionParameterSet CreateDefault(Guid setId) => new()
    {
        ParameterSetId = setId,
        DefaultSymbol = "ES",
        Symbols = [new() { Symbol = "ES", ShortLegDelta = 16, SpreadWidth = 50 }]
    };
    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion) =>
        OptionSpreadStrategyParameterValidation.Validate<VerticalSpreadMarketSelectionParameterSet>(
            ComponentCode, payloadJson, schemaVersion, value =>
            {
                var issues = OptionSpreadStrategyParameterValidation.Common(value.SchemaVersion,
                    value.ParameterSetId, value.Version, value.DefaultSymbol, value.Symbols.Select(x => x.Symbol));
                foreach (var (symbol, index) in value.Symbols.Select((symbol, index) => (symbol, index)))
                {
                    OptionSpreadStrategyParameterValidation.Delta(symbol.ShortLegDelta, $"Symbols/{index}/ShortLegDelta", issues);
                    OptionSpreadStrategyParameterValidation.Width(symbol.SpreadWidth, $"Symbols/{index}/SpreadWidth", issues);
                }
                return issues;
            });
}

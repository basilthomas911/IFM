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

static class OptionSpreadStrategyParameterValidation
{
    internal static ParameterValidationIssue[] Validate<T>(string component, string json, int schemaVersion,
        Func<T, List<ParameterValidationIssue>> validate)
    {
        try
        {
            var canonical = ParameterCanonicalPayloadModel.Canonicalize(json);
            var structural = ParameterSchemaRegistry.Default.ValidateStructure(component, schemaVersion, canonical);
            if (structural.Length != 0) return structural;
            var value = JsonSerializer.Deserialize<T>(canonical);
            return value is null ? [new("PARAM.OBJECT_REQUIRED", "Payload", "A parameter object is required.")] : validate(value).ToArray();
        }
        catch (Exception error) when (error is ArgumentException or JsonException)
        {
            return [new("PARAM.STRUCTURE_INVALID", "Payload", error.Message)];
        }
    }

    internal static List<ParameterValidationIssue> Common(int actualSchema, Guid setId, int version,
        string defaultSymbol, IEnumerable<string> symbols)
    {
        var issues = new List<ParameterValidationIssue>();
        if (actualSchema != 1) issues.Add(new("PARAM.SCHEMA_MISMATCH", "SchemaVersion", "Schema version must be one."));
        if (setId == Guid.Empty) issues.Add(new("PARAM.IDENTITY_INVALID", "ParameterSetId", "Parameter-set identity is required."));
        if (version < 0) issues.Add(new("PARAM.VERSION_INVALID", "Version", "Version cannot be negative."));
        var values = symbols.ToArray();
        if (values.Length == 0) issues.Add(new("PARAM.VALUE_INVALID", "Symbols", "At least one symbol profile is required."));
        if (values.Any(x => string.IsNullOrWhiteSpace(x) || x != x.Trim().ToUpperInvariant()))
            issues.Add(new("PARAM.VALUE_INVALID", "Symbols", "Symbols must be non-empty uppercase roots."));
        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
            issues.Add(new("PARAM.VALUE_INVALID", "Symbols", "Symbol profiles must be unique."));
        if (string.IsNullOrWhiteSpace(defaultSymbol) || !values.Contains(defaultSymbol, StringComparer.OrdinalIgnoreCase))
            issues.Add(new("PARAM.VALUE_INVALID", "DefaultSymbol", "DefaultSymbol must identify one configured symbol profile."));
        return issues;
    }

    internal static void Delta(int value, string path, ICollection<ParameterValidationIssue> issues)
    { if (value is < 1 or > 100) issues.Add(new("PARAM.VALUE_INVALID", path, "Delta must be between 1 and 100.")); }
    internal static void Width(decimal value, string path, ICollection<ParameterValidationIssue> issues)
    { if (value <= 0) issues.Add(new("PARAM.VALUE_INVALID", path, "Spread width must be positive.")); }
}

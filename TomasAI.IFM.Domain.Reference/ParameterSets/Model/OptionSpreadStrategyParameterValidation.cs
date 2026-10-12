using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

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
        if (values.Any(x => string.IsNullOrWhiteSpace(x) || !string.Equals(x,x.Trim().ToUpperInvariant(),StringComparison.Ordinal)))
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

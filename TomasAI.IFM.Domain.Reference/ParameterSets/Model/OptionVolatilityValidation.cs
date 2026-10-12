using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

static class OptionVolatilityValidation
{
    internal static void RequireSetId(Guid setId)
    {
        if (setId == Guid.Empty) throw new ArgumentException("Set identity is required.", nameof(setId));
    }

    internal static ParameterValidationIssue[] Validate<T>(
        string componentCode, string payloadJson, int schemaVersion, Action<T, List<ParameterValidationIssue>> validate)
    {
        try
        {
            var canonical = ParameterCanonicalPayloadModel.Canonicalize(payloadJson);
            var structural = ParameterSchemaRegistry.Default.ValidateStructure(componentCode, schemaVersion, canonical);
            if (structural.Length != 0) return structural;
            var value = JsonSerializer.Deserialize<T>(canonical);
            if (value is null) return [new("PARAM.OBJECT_REQUIRED", "Payload", "A parameter object is required.")];
            var issues = new List<ParameterValidationIssue>();
            validate(value, issues);
            if (typeof(T).GetProperty(nameof(OptionVolatilitySeriesParameterSet.SchemaVersion))?.GetValue(value) is int declared
                && declared != schemaVersion)
                issues.Add(new("PARAM.SCHEMA_MISMATCH", "SchemaVersion", "Payload schema differs from selected schema."));
            return issues.ToArray();
        }
        catch (Exception error) when (error is ArgumentException or JsonException)
        {
            return [new("PARAM.STRUCTURE_INVALID", "Payload", error.Message)];
        }
    }

    internal static void Common(int schemaVersion, Guid setId, int version, List<ParameterValidationIssue> issues)
    {
        if (schemaVersion != 1) Add(issues, "SchemaVersion", "Only schema version 1 is supported.", "PARAM.SCHEMA_MISMATCH");
        if (setId == Guid.Empty) Add(issues, "ParameterSetId", "Parameter-set identity is required.", "PARAM.IDENTITY_INVALID");
        if (version <= 0) Add(issues, "Version", "Parameter-set version must be positive.", "PARAM.VERSION_INVALID");
    }

    internal static void Required(string? value, string path, List<ParameterValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)) Add(issues, path, "A non-empty value is required.");
    }

    internal static void RequiredEnum<T>(T value, string path, List<ParameterValidationIssue> issues) where T : struct, Enum
    {
        if (Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 0) Add(issues, path, "A defined non-unknown value is required.");
    }

    internal static void Positive(int value, string path, List<ParameterValidationIssue> issues) =>
        InRange(value, 1, int.MaxValue, path, issues);

    internal static void InRange(int value, int minimum, int maximum, string path, List<ParameterValidationIssue> issues)
    {
        if (value < minimum || value > maximum) Add(issues, path, $"Value must be between {minimum} and {maximum}.");
    }

    internal static void OptionalRange(decimal? value, decimal minimum, decimal maximum, string path, List<ParameterValidationIssue> issues)
    {
        if (value < minimum || value > maximum) Add(issues, path, $"Value must be between {minimum} and {maximum}.");
    }

    internal static void OptionalMinimum(decimal? value, decimal minimum, string path, List<ParameterValidationIssue> issues)
    {
        if (value < minimum) Add(issues, path, $"Value must be at least {minimum}.");
    }

    internal static void UniqueRequired(string[]? values, string path, List<ParameterValidationIssue> issues)
    {
        if (values is not { Length: > 0 } || values.Any(string.IsNullOrWhiteSpace))
            Add(issues, path, "At least one non-empty value is required.");
        else if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            Add(issues, path, "Values must be unique.");
    }

    internal static void UniqueDefined<T>(T[]? values, string path, List<ParameterValidationIssue> issues) where T : struct, Enum
    {
        if (values is not { Length: > 0 } || values.Any(value => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 0))
            Add(issues, path, "At least one defined value is required.");
        else if (values.Distinct().Count() != values.Length)
            Add(issues, path, "Values must be unique.");
    }

    internal static void Add(List<ParameterValidationIssue> issues, string path, string message, string code = "PARAM.VALUE_INVALID") =>
        issues.Add(new(code, path, message));
}

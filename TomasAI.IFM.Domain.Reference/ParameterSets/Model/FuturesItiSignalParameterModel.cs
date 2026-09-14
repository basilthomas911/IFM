using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Defines authoring, schema, and validation behavior for Futures ITI parameters.</summary>
public sealed class FuturesItiSignalParameterModel : IParameterComponentDescriptor
{
    /// <summary>Gets the stable component code stored by the generic parameter-set catalog.</summary>
    public const string ComponentCode = ParameterSchemaRegistry.FuturesItiSignalComponent;

    /// <inheritdoc />
    public ParameterComponentSummary Summary => new(
        "market-data-analytics",
        "Market Data Analytics",
        ComponentCode,
        "Future ITI Signal",
        [ParameterSchemaRegistry.CurrentFuturesItiSignalSchemaVersion],
        true);

    /// <inheritdoc />
    public string CreateDraftPayload(Guid setId)
    {
        if (setId == Guid.Empty)
            throw new ArgumentException("Set identity is required.", nameof(setId));

        return JsonSerializer.Serialize(new FuturesItiSignalParameterSet
        {
            ParameterSetId = setId,
            DefaultTradingDays = new()
            {
                Daily = 1,
                Weekly = 10,
                Monthly = 30
            }
        });
    }

    /// <inheritdoc />
    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion)
    {
        try
        {
            var canonical = ParameterCanonicalPayloadModel.Canonicalize(payloadJson);
            var structural = ParameterSchemaRegistry.Default.ValidateStructure(
                ComponentCode,
                schemaVersion,
                canonical);
            if (structural.Length != 0)
                return structural;

            var value = JsonSerializer.Deserialize<FuturesItiSignalParameterSet>(canonical);
            if (value is null)
                return [new("PARAM.OBJECT_REQUIRED", "Payload", "A parameter object is required.")];

            var issues = new List<ParameterValidationIssue>();
            if (value.SchemaVersion != schemaVersion)
                issues.Add(new("PARAM.SCHEMA_MISMATCH", "SchemaVersion", "Payload schema differs from selected schema."));
            if (value.ParameterSetId == Guid.Empty)
                issues.Add(new("PARAM.IDENTITY_INVALID", "ParameterSetId", "Parameter-set identity is required."));
            if (value.Version < 0)
                issues.Add(new("PARAM.VERSION_INVALID", "Version", "Parameter-set version cannot be negative."));
            if (value.DefaultTradingDays.Daily <= 0)
                issues.Add(new("PARAM.VALUE_INVALID", "DefaultTradingDays/Daily", "Daily trading days must be positive."));
            if (value.DefaultTradingDays.Weekly <= 0)
                issues.Add(new("PARAM.VALUE_INVALID", "DefaultTradingDays/Weekly", "Weekly trading days must be positive."));
            if (value.DefaultTradingDays.Monthly <= 0)
                issues.Add(new("PARAM.VALUE_INVALID", "DefaultTradingDays/Monthly", "Monthly trading days must be positive."));
            return issues.ToArray();
        }
        catch (Exception error) when (error is ArgumentException or JsonException)
        {
            return [new("PARAM.STRUCTURE_INVALID", "Payload", error.Message)];
        }
    }
}

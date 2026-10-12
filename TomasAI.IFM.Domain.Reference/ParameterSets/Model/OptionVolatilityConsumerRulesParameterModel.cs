using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

public sealed class OptionVolatilityConsumerRulesParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.OptionVolatilityConsumerRulesComponent;
    public ParameterComponentSummary Summary => new(
        "option-volatility", "Option Volatility", ComponentCode, "Consumer Rules",
        [ParameterSchemaRegistry.CurrentOptionVolatilityConsumerRulesSchemaVersion], true);

    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateDefault(setId));

    public static OptionVolatilityConsumerRulesParameterSet CreateDefault(Guid setId)
    {
        OptionVolatilityValidation.RequireSetId(setId);
        return new() { ParameterSetId = setId };
    }

    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion) =>
        OptionVolatilityValidation.Validate<OptionVolatilityConsumerRulesParameterSet>(
            ComponentCode, payloadJson, schemaVersion, ValidateValue);

    static void ValidateValue(OptionVolatilityConsumerRulesParameterSet value, List<ParameterValidationIssue> issues)
    {
        OptionVolatilityValidation.Common(value.SchemaVersion, value.ParameterSetId, value.Version, issues);
        OptionVolatilityValidation.Required(value.SeriesId, "SeriesId", issues);
        OptionVolatilityValidation.Required(value.MethodologyVersion, "MethodologyVersion", issues);
        OptionVolatilityValidation.Required(value.MetricPolicyVersion, "MetricPolicyVersion", issues);
        OptionVolatilityValidation.RequiredEnum(value.OpenMarketEvidence, "OpenMarketEvidence", issues);
        OptionVolatilityValidation.RequiredEnum(value.ClosedMarketEvidence, "ClosedMarketEvidence", issues);
        OptionVolatilityValidation.RequiredEnum(value.UnavailablePolicy, "UnavailablePolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.IronCondor, "IronCondor", issues);
        OptionVolatilityValidation.RequiredEnum(value.VerticalSpread, "VerticalSpread", issues);
        OptionVolatilityValidation.RequiredEnum(value.FuturesOutright, "FuturesOutright", issues);
        OptionVolatilityValidation.RequiredEnum(value.HeldOptions, "HeldOptions", issues);
        OptionVolatilityValidation.RequiredEnum(value.ProtectiveClose, "ProtectiveClose", issues);
        OptionVolatilityValidation.RequiredEnum(value.ProtectiveCancel, "ProtectiveCancel", issues);
        OptionVolatilityValidation.Positive(value.LiveMaximumAgeMinutes, "LiveMaximumAgeMinutes", issues);
        OptionVolatilityValidation.InRange(value.MaximumSessionLag, 0, 30, "MaximumSessionLag", issues);
        if (!value.ExactDecisionSnapshotRequired)
            OptionVolatilityValidation.Add(issues, "ExactDecisionSnapshotRequired", "Consumer decisions must retain the exact accepted snapshot.");
        if (value.AllowDownstreamSilentReplacement)
            OptionVolatilityValidation.Add(issues, "AllowDownstreamSilentReplacement", "Downstream consumers cannot silently replace accepted evidence.");
        if (value.NumericalRules is null)
        {
            OptionVolatilityValidation.Add(issues, "NumericalRules", "Numerical rules are required, even when every value is null.");
            return;
        }
        var rules = value.NumericalRules;
        OptionVolatilityValidation.OptionalRange(rules.MinimumIvRank, 0, 100, "NumericalRules/MinimumIvRank", issues);
        OptionVolatilityValidation.OptionalRange(rules.MaximumIvRank, 0, 100, "NumericalRules/MaximumIvRank", issues);
        OptionVolatilityValidation.OptionalRange(rules.MinimumIvPercentile, 0, 100, "NumericalRules/MinimumIvPercentile", issues);
        OptionVolatilityValidation.OptionalRange(rules.MaximumIvPercentile, 0, 100, "NumericalRules/MaximumIvPercentile", issues);
        OptionVolatilityValidation.OptionalRange(rules.DeltaTargetAdjustment, -1, 1, "NumericalRules/DeltaTargetAdjustment", issues);
        OptionVolatilityValidation.OptionalMinimum(rules.SpreadWidthAdjustment, 0, "NumericalRules/SpreadWidthAdjustment", issues);
        OptionVolatilityValidation.OptionalMinimum(rules.RiskLimitAdjustment, 0, "NumericalRules/RiskLimitAdjustment", issues);
        if (rules.MinimumIvRank > rules.MaximumIvRank)
            OptionVolatilityValidation.Add(issues, "NumericalRules", "Minimum IV Rank cannot exceed maximum IV Rank.");
        if (rules.MinimumIvPercentile > rules.MaximumIvPercentile)
            OptionVolatilityValidation.Add(issues, "NumericalRules", "Minimum IV Percentile cannot exceed maximum IV Percentile.");
    }
}

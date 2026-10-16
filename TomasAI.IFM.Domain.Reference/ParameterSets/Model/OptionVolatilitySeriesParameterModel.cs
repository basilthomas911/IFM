using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;
public sealed class OptionVolatilitySeriesParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = ParameterSchemaRegistry.OptionVolatilitySeriesComponent;
    public ParameterComponentSummary Summary => new("option-volatility", "Option Volatility", ComponentCode, "Series", [ParameterSchemaRegistry.CurrentOptionVolatilitySeriesSchemaVersion], true);

    public ParameterSetValue CreateTypedDraft(Guid setId)=>ParameterSetValue.From(CreateDefault(setId));
    public ParameterValidationIssue[] ValidateTyped(ParameterSetValue parameters,int schemaVersion)
    {
        if(parameters.ComponentCode!=ComponentCode||parameters.SchemaVersion!=schemaVersion)return [new("PARAM.SCHEMA_MISMATCH","Parameters","The typed configuration does not match the component or schema")];
        var issues=new List<ParameterValidationIssue>();ValidateValue(parameters.Get<OptionVolatilitySeriesParameterSet>(),issues);return issues.ToArray();
    }
    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateDefault(setId));
    public static OptionVolatilitySeriesParameterSet CreateDefault(Guid setId)
    {
        OptionVolatilityValidation.RequireSetId(setId);
        return new()
        {
            ParameterSetId = setId
        };
    }

    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion) => OptionVolatilityValidation.Validate<OptionVolatilitySeriesParameterSet>(ComponentCode, payloadJson, schemaVersion, ValidateValue);
    static void ValidateValue(OptionVolatilitySeriesParameterSet value, List<ParameterValidationIssue> issues)
    {
        OptionVolatilityValidation.Common(value.SchemaVersion, value.ParameterSetId, value.Version, issues);
        OptionVolatilityValidation.Required(value.SeriesId, "SeriesId", issues);
        OptionVolatilityValidation.Required(value.MethodologyVersion, "MethodologyVersion", issues);
        OptionVolatilityValidation.Required(value.UnderlyingRoot, "UnderlyingRoot", issues);
        OptionVolatilityValidation.Required(value.Venue, "Venue", issues);
        OptionVolatilityValidation.Required(value.Currency, "Currency", issues);
        OptionVolatilityValidation.Required(value.MarketDataProvider, "MarketDataProvider", issues);
        OptionVolatilityValidation.Required(value.Dataset, "Dataset", issues);
        OptionVolatilityValidation.Required(value.ExchangeCalendar, "ExchangeCalendar", issues);
        OptionVolatilityValidation.Required(value.TimeZone, "TimeZone", issues);
        ValidateConventions(value, issues);
        OptionVolatilityValidation.Positive(value.TargetMaturityCalendarDays, "TargetMaturityCalendarDays", issues);
        OptionVolatilityValidation.Positive(value.MinimumDisplayedSize, "MinimumDisplayedSize", issues);
        OptionVolatilityValidation.Positive(value.MaximumQuoteAgeSeconds, "MaximumQuoteAgeSeconds", issues);
        OptionVolatilityValidation.Positive(value.MaximumIvAgeSeconds, "MaximumIvAgeSeconds", issues);
        OptionVolatilityValidation.Positive(value.MaximumQuoteSkewMilliseconds, "MaximumQuoteSkewMilliseconds", issues);
        OptionVolatilityValidation.Positive(value.IntradayCoalescingMinutes, "IntradayCoalescingMinutes", issues);
        OptionVolatilityValidation.InRange(value.MaximumIntradayCheckpoints, 1, 84, "MaximumIntradayCheckpoints", issues);
        OptionVolatilityValidation.Positive(value.HistoricalLookbackSessions, "HistoricalLookbackSessions", issues);
        OptionVolatilityValidation.InRange(value.MinimumValidObservations, 1, value.HistoricalLookbackSessions, "MinimumValidObservations", issues);
        if (value.MinimumCoverage is <= 0 or > 1)
            OptionVolatilityValidation.Add(issues, "MinimumCoverage", "Minimum coverage must be greater than zero and no greater than one.");
        OptionVolatilityValidation.UniqueRequired(value.EligibleProductFamilies, "EligibleProductFamilies", issues);
        if (!string.IsNullOrWhiteSpace(value.TimeZone))
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(value.TimeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                OptionVolatilityValidation.Add(issues, "TimeZone", "Time zone is not recognized.");
            }
            catch (InvalidTimeZoneException)
            {
                OptionVolatilityValidation.Add(issues, "TimeZone", "Time zone is invalid.");
            }
        }

        ValidatePricerSelections(value, issues);
    }

    /// <summary>Requires explicit supported volatility environment, quoting, sampling and calculation conventions.</summary>
    static void ValidateConventions(OptionVolatilitySeriesParameterSet value, List<ParameterValidationIssue> issues)
    {
        OptionVolatilityValidation.RequiredEnum(value.Environment, "Environment", issues);
        OptionVolatilityValidation.RequiredEnum(value.AtmConvention, "AtmConvention", issues);
        OptionVolatilityValidation.RequiredEnum(value.OptionCombination, "OptionCombination", issues);
        OptionVolatilityValidation.RequiredEnum(value.ExpirySelection, "ExpirySelection", issues);
        OptionVolatilityValidation.RequiredEnum(value.Interpolation, "Interpolation", issues);
        OptionVolatilityValidation.RequiredEnum(value.UnderlyingMatch, "UnderlyingMatch", issues);
        OptionVolatilityValidation.RequiredEnum(value.QuoteMark, "QuoteMark", issues);
        OptionVolatilityValidation.RequiredEnum(value.CrossedMarketPolicy, "CrossedMarketPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.LockedMarketPolicy, "LockedMarketPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.RollPolicy, "RollPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.DailySamplingPolicy, "DailySamplingPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.GapPolicy, "GapPolicy", issues);
        OptionVolatilityValidation.RequiredEnum(value.RankBounds, "RankBounds", issues);
        OptionVolatilityValidation.RequiredEnum(value.PercentileConvention, "PercentileConvention", issues);
        OptionVolatilityValidation.RequiredEnum(value.ImpliedVolatilityUnit, "ImpliedVolatilityUnit", issues);
        OptionVolatilityValidation.RequiredEnum(value.RankAndPercentileUnit, "RankAndPercentileUnit", issues);
    }

    /// <summary>Requires unique exercise styles and explicit supported pricer versions for both European and American options.</summary>
    static void ValidatePricerSelections(OptionVolatilitySeriesParameterSet value, List<ParameterValidationIssue> issues)
    {
        if (value.Pricers is not { Length: > 0 })
            OptionVolatilityValidation.Add(issues, "Pricers", "At least one exercise-style pricer selection is required.");
        else
        {
            if (value.Pricers.Any(selection => selection is null))
                OptionVolatilityValidation.Add(issues, "Pricers", "Pricer selections cannot be null.");
            var selections = value.Pricers.Where(selection => selection is not null).ToArray();
            if (selections.Select(selection => selection.ExerciseStyle).Distinct().Count() != selections.Length)
                OptionVolatilityValidation.Add(issues, "Pricers", "Exercise styles must be unique.");
            foreach (var selection in selections)
            {
                if (selection.ExerciseStyle == OptionVolatilityExerciseStyle.Unknown)
                    OptionVolatilityValidation.Add(issues, "Pricers/ExerciseStyle", "An exercise style is required.");
                OptionVolatilityValidation.UniqueRequired(selection.PricerVersions, "Pricers/PricerVersions", issues);
            }

            foreach (var style in (new[]
            {
                OptionVolatilityExerciseStyle.European,
                OptionVolatilityExerciseStyle.American
            }

            ).Where(style => selections.All(selection => selection.ExerciseStyle != style)))
                OptionVolatilityValidation.Add(issues, "Pricers", $"A {style} pricer selection is required.");
        }
    }
}

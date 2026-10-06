using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Validation;

/// <summary>Aggregate historical loader identity and payload validation.</summary>
public static class HistoricalDataLoaderCommandValidation
{
    /// <summary>Validates the intrinsic data-load attempt identity.</summary>
    public static List<ValidationError> ValidateDataLoadAttemptId(this List<ValidationError> errors, FuturesAnalyticsHistoricalDataLoaderEntityId id)
    {
        if (id.Value == Guid.Empty) errors.Add(new("DataLoadAttemptId is required."));
        return errors;
    }

    /// <summary>Validates all inclusive date, budget, version and series requirements without throwing.</summary>
    public static List<ValidationError> ValidateHistoricalLoad(this List<ValidationError> errors, LoadFuturesAnalyticsHistoricalDataCommand value)
    {

        if (value.CommandId == Guid.Empty || value.EntityId.Value == Guid.Empty
            || value.CommandId != value.EntityId.Value)
            errors.Add(new("CommandId and DataLoadAttemptId must be the same non-empty identity."));
        if (value.FuturesAnalyticsHistoricalDataLoaderParameters is null) { errors.Add(new("FuturesAnalyticsHistoricalDataLoaderParameters are required.")); return errors; }
        if (value.FuturesAnalyticsHistoricalDataLoaderParameters.StartDate == default || value.FuturesAnalyticsHistoricalDataLoaderParameters.EndDate < value.FuturesAnalyticsHistoricalDataLoaderParameters.StartDate)
            errors.Add(new("A valid inclusive data load date range is required."));
        if (value.FuturesAnalyticsHistoricalDataLoaderParameters.Series is null || value.FuturesAnalyticsHistoricalDataLoaderParameters.Series.Length == 0)
            errors.Add(new("At least one historical series is required."));
        if (value.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumCostUsd <= 0 || value.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumBytes <= 0)
            errors.Add(new("Positive cost and byte budgets are required."));
        if (string.IsNullOrWhiteSpace(value.FuturesAnalyticsHistoricalDataLoaderParameters.NormalizationVersion)
            || string.IsNullOrWhiteSpace(value.FuturesAnalyticsHistoricalDataLoaderParameters.CalculationConfigurationVersion)
            || string.IsNullOrWhiteSpace(value.FuturesAnalyticsHistoricalDataLoaderParameters.RequestedBy))
            errors.Add(new("Normalization, calculation configuration, and requester are required."));
        foreach (var series in value.FuturesAnalyticsHistoricalDataLoaderParameters.Series ?? [])
        {
            if (series is null) { errors.Add(new("Historical series identity is required.")); continue; }
            if (new MarketSeriesIdentityValidationRules().Execute(series.MarketSeriesIdentity).Length != 0)
                errors.Add(new("Every data load series identity must be valid."));
            if (!Enum.IsDefined(series.Schema))
                errors.Add(new("Every data load historical schema must be supported."));
        }
        return errors;
    }
}

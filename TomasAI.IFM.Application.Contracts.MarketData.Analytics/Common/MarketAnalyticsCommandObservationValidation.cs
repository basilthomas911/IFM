using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Validates optional closed-bar input shared by legacy and checkpoint analytics commands.</summary>
public static class MarketAnalyticsCommandObservationValidation
{
    /// <summary>Adds source-bar shape and identity errors when a command carries an observation.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing public concrete validation collection contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing public concrete validation collection contract.")]
    public static List<ValidationError> ValidateClosedObservation(
        this List<ValidationError> errors,
        FuturesTradeSessionBarReadModel? observation,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timeFrame,
        string commandName,
        bool required = false,
        bool allowPriorValueDate = false)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (observation is null)
        {
            if (required)
                errors.Add(new("ANALYTICS.BAR.REQUIRED", $"{commandName}: a source bar is required."));
            return errors;
        }
        foreach (var error in new FuturesTradeSessionBarReadModelValidationRules().Execute(observation))
            errors.Add(new(error.ErrorCode, $"{commandName}.Observation: {error.ErrorMessage}"));
        if (!observation.IsComplete || !observation.IsValid)
            errors.Add(new("ANALYTICS.BAR.INVALID", $"{commandName}: a valid completed bar is required."));
        if (!string.Equals(observation.ContractId,contractId,StringComparison.Ordinal)
            || (!allowPriorValueDate && observation.ValueDate != valueDate)
            || (allowPriorValueDate && observation.ValueDate > valueDate)
            || observation.TimeFrame != timeFrame)
            errors.Add(new("ANALYTICS.BAR.IDENTITY", $"{commandName}: source bar contract, date, and timeframe must match."));
        return errors;
    }
}

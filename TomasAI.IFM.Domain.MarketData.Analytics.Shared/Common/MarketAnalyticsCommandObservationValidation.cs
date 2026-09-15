using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Validates optional closed-bar input shared by legacy and checkpoint analytics commands.</summary>
public static class MarketAnalyticsCommandObservationValidation
{
    /// <summary>Adds source-bar shape and identity errors when a command carries an observation.</summary>
    public static List<ValidationError> ValidateClosedObservation(
        this List<ValidationError> errors,
        FuturesTradeSessionBarReadModel? observation,
        string contractId,
        DateOnly valueDate,
        TimeFrameType timeFrame,
        string commandName,
        bool required = false)
    {
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
        if (observation.ContractId != contractId || observation.ValueDate != valueDate
            || observation.TimeFrame != timeFrame)
            errors.Add(new("ANALYTICS.BAR.IDENTITY", $"{commandName}: source bar contract, date, and timeframe must match."));
        return errors;
    }
}

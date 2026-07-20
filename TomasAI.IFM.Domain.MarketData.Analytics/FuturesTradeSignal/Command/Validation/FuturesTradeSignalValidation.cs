using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Validation;

internal static class FuturesTradeSignalValidation
{
    public static List<ValidationError> ValidateFuturesEodData(this List<ValidationError> validationErrors, FuturesEodDataV2ReadModel futuresEodData)
    {
        var validator = new FuturesEodDataValidationRules();
        var ruleErrors = validator.Execute(futuresEodData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    public static List<ValidationError> ValidateFuturesRsiSignal(this List<ValidationError> validationErrors, FuturesRsiSignalReadModel futuresRsiSignals)
    {
        if (futuresRsiSignals is null)
            return validationErrors;

        var validator = new FuturesRsiSignalReadModelValidationRules();
        var ruleErrors = validator.Execute(futuresRsiSignals);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    public static List<ValidationError> ValidateFuturesTdiSignal(this List<ValidationError> validationErrors, FuturesTdiSignalReadModel futuresTdiSignals)
    {
        if (futuresTdiSignals is null)
            return validationErrors;

        var validator = new FuturesTdiSignalReadModelValidationRules();
        var ruleErrors = validator.Execute(futuresTdiSignals);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    public static List<ValidationError> ValidateFuturesItiSignalData(this List<ValidationError> validationErrors, FuturesItiSignalDataReadModel futuresItiSignals)
    {
        if (futuresItiSignals is null)
            return validationErrors;

        var validator = new FuturesItiSignalV2ReadModelValidationRules();
        var ruleErrors = validator.Execute(futuresItiSignals.TrendDirectionChange);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);

        ruleErrors = validator.Execute(futuresItiSignals.TrendExtremeChange);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);

        ruleErrors = validator.Execute(futuresItiSignals.TrendReversalChange);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates that the VIX futures price is a valid positive number within a reasonable range.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="vixFuturesPrice">The VIX futures price to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateVixFuturesPrice(this List<ValidationError> validationErrors, decimal vixFuturesPrice, string commandName)
    {
        if (vixFuturesPrice <= 0m)
            validationErrors.Add(new ValidationError($"{commandName}.VixFuturesPrice must be greater than zero"));

        // VIX futures prices are typically between 0 and 200; values outside this range are likely errors
        if (vixFuturesPrice > 200m)
            validationErrors.Add(new ValidationError($"{commandName}.VixFuturesPrice exceeds reasonable maximum of 200"));

        // Check for extreme precision issues that might indicate invalid data
        if (vixFuturesPrice > 0m && vixFuturesPrice < 0.01m)
            validationErrors.Add(new ValidationError($"{commandName}.VixFuturesPrice is unreasonably low (< 0.01)"));

        return validationErrors;
    }

    /// <summary>
    /// Validates that the TimePeriod is defined and not set to an invalid enum value.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="timePeriod">The time period to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateTimePeriod(this List<ValidationError> validationErrors, TradeTimePeriodType timePeriod, string commandName)
    {
        if (!Enum.IsDefined(typeof(TradeTimePeriodType), timePeriod))
            validationErrors.Add(new ValidationError($"{commandName}.TimePeriod has an invalid value"));
        return validationErrors;
    }



}

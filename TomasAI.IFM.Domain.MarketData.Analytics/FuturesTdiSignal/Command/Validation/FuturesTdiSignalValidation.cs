using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Validation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Validation;

internal static class FuturesTdiSignalValidation
{

    public static List<ValidationError> ValidateFuturesTdiSignalId(this List<ValidationError> validationErrors, FuturesTdiSignalId futuresTdiSignalId)
    {
        var validator = new FuturesTdiSignalIdValidationRules();
        var ruleErrors = validator.Execute(futuresTdiSignalId);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    public static List<ValidationError> ValidateFuturesTdiSignalReadModel(this List<ValidationError> validationErrors, FuturesTdiSignalReadModel futuresTdiSignal)
    {
        if (futuresTdiSignal is null)
        {
            validationErrors.Add(new ValidationError("FuturesTdiSignal is null"));
            return validationErrors;
        }

        var validator = new FuturesTdiSignalReadModelValidationRules();
        var ruleErrors = validator.Execute(futuresTdiSignal);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    public static List<ValidationError> ValidateFuturesRsiSignals(this List<ValidationError> validationErrors, FuturesRsiSignalReadModel[] futuresRsiSignals)
    {
        if (futuresRsiSignals is null || futuresRsiSignals.Length == 0)
        {
            validationErrors.Add(new ValidationError("FuturesRsiSignals array is null or empty"));
            return validationErrors;
        }

        var validator = new FuturesRsiSignalReadModelValidationRules();
        for (int i = 0; i < futuresRsiSignals.Length; i++)
        {
            var ruleErrors = validator.Execute(futuresRsiSignals[i]);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        return validationErrors;
    }
}

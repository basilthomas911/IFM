using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Validation;
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

    public static List<ValidationError> ValidateFuturesTdiConfiguration(
        this List<ValidationError> validationErrors,
        FuturesTdiConfiguration configuration,
        FuturesTdiSignalEntityId entityId,
        FuturesRsiSignalReadModel[] futuresRsiSignals)
    {
        var ruleErrors = new FuturesTdiConfigurationValidationRules().Execute(configuration);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);

        if (!FuturesTdiConfiguration.IsSupportedIntraday(entityId.TimePeriod))
            validationErrors.Add(new ValidationError("Traders Dynamic Index supports intraday time periods only"));
        if (!string.Equals(entityId.ConfigurationId, configuration.ConfigurationId, StringComparison.Ordinal))
            validationErrors.Add(new ValidationError("TDI entity configuration does not match the command configuration"));
        if (futuresRsiSignals.Length < configuration.RequiredRsiSamples)
            validationErrors.Add(new ValidationError($"TDI requires at least {configuration.RequiredRsiSamples} RSI samples"));

        foreach (var signal in futuresRsiSignals)
        {
            if (!string.Equals(signal.ContractId, entityId.ContractId, StringComparison.Ordinal)
                || signal.ValueDate != entityId.ValueDate
                || signal.TimePeriod != entityId.TimePeriod
                || signal.PeriodLength != configuration.RsiPeriod)
            {
                validationErrors.Add(new ValidationError("Every RSI sample must match the TDI contract, value date, time period, and RSI period"));
                break;
            }
        }
        return validationErrors;
    }
}

using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Validation;

public static class FuturesTradeSignalValidationExtensions
{
    public static List<ValidationError> ValidateFuturesTradeSignal(this List<ValidationError> validationErrors, FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        var ruleErrors = new FuturesTradeSignalValidationRules().Execute(futuresTradeSignal);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}

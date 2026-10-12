using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Validation;

public static class FuturesTradeSignalValidationExtensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing public concrete validation collection contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing public concrete validation collection contract.")]
    public static List<ValidationError> ValidateFuturesTradeSignal(this List<ValidationError> validationErrors, FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        var ruleErrors = new FuturesTradeSignalValidationRules().Execute(futuresTradeSignal);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}

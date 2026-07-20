using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.Validation;

public static  class TradePlanForwardLossLimitValidationExtensions
{
    /// <summary>
    /// Validates the forward loss limit settings of a trade plan and appends any validation errors to the provided
    /// list.
    /// </summary>
    /// <remarks>This method uses predefined validation rules to check the forward loss limit settings.  If
    /// validation errors are found, they are appended to the provided <paramref name="validationErrors"/>
    /// list.</remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="forwardLossLimit">The <see cref="TradePlanForwardLossLimitReadModel"/> object representing the forward loss limit settings to
    /// validate.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during validation.</returns>
    public static  List<ValidationError> ValidateTradePlanForwardLossLimit(this List<ValidationError> validationErrors, TradePlanForwardLossLimitReadModel forwardLossLimit)
    {
        var ruleErrors = new TradePlanForwardLossLimitValidationRules().Execute(forwardLossLimit);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

}

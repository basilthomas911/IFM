using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Validation;

/// <summary>
/// Provides validation extension methods for futures RSI signal-related domain objects used within the RSI signal
/// command actor.
/// </summary>
/// <remarks>
/// This static class contains extension methods that validate RSI signal identifiers and related EOD market data
/// required for RSI computation. Each method extends <see cref="List{ValidationError}"/> to allow fluent chaining
/// of validation calls within the actor's command validation pipeline. Validation rules are delegated to specialized
/// validator classes (<see cref="FuturesRsiSignalEntityIdValidationRules"/> and <see
/// cref="FuturesEodDataV2ReadModelValidationRules"/>) that encapsulate the detailed validation logic using
/// FluentValidation. All methods modify the provided error list in place and return it for convenience.
/// </remarks>
internal static class FuturesRsiSignalValidation
{
    /// <summary>
    /// Validates the specified <see cref="FuturesRsiSignalEntityId"/> and adds any validation errors to the provided
    /// collection.
    /// </summary>
    /// <remarks>
    /// This method extends a list of validation errors by validating a futures RSI signal entity identifier using <see
    /// cref="FuturesRsiSignalEntityIdValidationRules"/>. The validator checks that the contract identifier is not empty,
    /// the value date is within valid range (not MinValue or MaxValue), and the time period is a valid enum value other
    /// than <c>None</c>. Any validation errors discovered are appended to the <paramref name="validationErrors"/> list.
    /// The original list is modified in place and also returned for fluent chaining.
    /// </remarks>
    /// <param name="validationErrors">The list to which any validation errors found during validation will be added. Cannot be null.</param>
    /// <param name="futuresRsiSignalId">The <see cref="FuturesRsiSignalEntityId"/> instance to validate. This parameter must not be null.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during validation
    /// of the specified RSI signal entity identifier.</returns>
    public static List<ValidationError> ValidateFuturesRsiSignalEntityId(this List<ValidationError> validationErrors, FuturesRsiSignalEntityId futuresRsiSignalId)
    {
        var validator = new FuturesRsiSignalEntityIdValidationRules();
        var ruleErrors = validator.Execute(futuresRsiSignalId);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="futuresRsiSignalId"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateFuturesRsiDailySignalEntityId(this List<ValidationError> validationErrors, FuturesRsiDailySignalEntityId futuresRsiSignalId)
    {
        var validator = new FuturesRsiDailySignalEntityIdValidationRules();
        var ruleErrors = validator.Execute(futuresRsiSignalId);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

}

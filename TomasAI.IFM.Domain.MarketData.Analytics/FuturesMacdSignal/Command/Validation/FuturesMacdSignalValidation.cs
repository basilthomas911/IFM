using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Validation;

/// <summary>
/// Provides validation extension methods for futures MACD signal-related domain objects used within the MACD signal
/// command actor.
/// </summary>
/// <remarks>
/// This static class contains extension methods that validate MACD signal identifiers and related RSI signal data
/// required for MACD computation. Each method extends <see cref="List{ValidationError}"/> to allow fluent chaining
/// of validation calls within the actor's command validation pipeline. Validation rules are delegated to specialized
/// validator classes (<see cref="FuturesMacdSignalIdValidationRules"/> and <see
/// cref="FuturesRsiSignalReadModelValidationRules"/>) that encapsulate the detailed validation logic using
/// FluentValidation. All methods modify the provided error list in place and return it for convenience.
/// </remarks>
internal static class FuturesMacdSignalValidation
{
    /// <summary>
    /// Validates the specified <see cref="FuturesMacdSignalId"/> and adds any validation errors to the provided
    /// collection with command-specific error message prefixes.
    /// </summary>
    /// <remarks>
    /// This method extends a list of validation errors by validating a futures MACD signal identifier. The validator
    /// checks that the contract identifier is not empty, the value date is within valid range, and the timestamp is
    /// within valid range. Error messages are prefixed with the <paramref name="commandName"/> to provide context
    /// within command validation pipelines. Any validation errors discovered are appended to the <paramref
    /// name="validationErrors"/> list. The original list is modified in place and also returned for fluent chaining.
    /// </remarks>
    /// <param name="validationErrors">The list to which any validation errors found during validation will be added. Cannot be null.</param>
    /// <param name="futuresMacdSignalId">The <see cref="FuturesMacdSignalId"/> instance to validate. This parameter must not be null.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during validation
    /// of the specified MACD signal identifier.</returns>
    public static List<ValidationError> ValidateFuturesMacdSignalId(
        this List<ValidationError> validationErrors, FuturesMacdSignalId futuresMacdSignalId)
    {
        var validationRules = new FuturesMacdSignalIdValidationRules();
        validationErrors.AddRange(validationRules.Execute(futuresMacdSignalId));
        return validationErrors;
    }
}

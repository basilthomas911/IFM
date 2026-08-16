using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Validation;

/// <summary>
/// Provides extension methods for validating economic calendar view models and economic calendar identifiers.
/// </summary>
/// <remarks>These extension methods add validation errors to an existing list based on the results of specific
/// validation rules. They are intended to be used as part of a validation workflow for economic calendars and their
/// identifiers.</remarks>
public static class EconomicCalendarValidationExtensions
{
    static readonly EconomicCalendarValidationRules EconomicCalendarRules = new();
    static readonly EconomicCalendarIdValidationRules EconomicCalendarIdRules = new();
    /// <summary>
    /// Validates an economic calendar view model.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to which any new errors will be added.</param>
    /// <param name="economicCalendar">The economic calendar view model to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateEconomicCalendar(this List<ValidationError> validationErrors, EconomicCalendarReadModel economicCalendar)
    {
        var ruleErrors = EconomicCalendarRules.Execute(economicCalendar);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>Validates the optional provider-neutral country filters on an import request.</summary>
    public static List<ValidationError> ValidateImportCountryCodes(
        this List<ValidationError> validationErrors,
        string[]? countryCodes,
        string commandName)
    {
        if (countryCodes is null)
        {
            validationErrors.Add(new ValidationError($"{commandName}.CountryCodes is null"));
            return validationErrors;
        }

        foreach (var countryCode in countryCodes)
            if (string.IsNullOrWhiteSpace(countryCode)
                || countryCode.Trim().Length is < 2 or > 3
                || countryCode.Trim().Any(character => !char.IsAsciiLetter(character)))
                validationErrors.Add(new ValidationError(
                    $"{commandName}.CountryCodes contains an invalid two- or three-letter country code"));
        return validationErrors;
    }

    /// <summary>
    /// Validates an economic calendar identifier.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to which any new errors will be added.</param>
    /// <param name="economicCalendarId">The economic calendar identifier to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateEconomicCalendarId(this List<ValidationError> validationErrors, EconomicCalendarId economicCalendarId)
    {
        var ruleErrors = EconomicCalendarIdRules.Execute(economicCalendarId);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}

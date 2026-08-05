using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Validation;

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

    public static List<ValidationError> ValidateEconomicCalendars(this List<ValidationError> validationErrors, EconomicCalendarReadModel[] economicCalendars, string commandName)
    {
        if (economicCalendars is null || economicCalendars.Length == 0)
            validationErrors.Add(new ValidationError($"{commandName}.EconomicCalendars is empty"));

        foreach (var e in economicCalendars!)
        {
            var ruleErrors = EconomicCalendarRules.Execute(e);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
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

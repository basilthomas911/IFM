using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Validation;

/// <summary>
/// Provides validation extension methods for Futures ITI Signal commands and related data models.
/// </summary>
/// <remarks>
/// Each validation method follows a consistent pattern: it accepts a list of validation errors,
/// the value to validate, and the command name for context, then returns the updated list of errors.
/// This allows for fluent chaining of multiple validations.
/// </remarks>
public static class FuturesItiSignalValidation
{
    /// <summary>
    /// Validates that the ContractId is not null or whitespace.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="contractId">The contract identifier to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static TErrors ValidateContractId<TErrors>(this TErrors validationErrors, string contractId, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (string.IsNullOrWhiteSpace(contractId))
            validationErrors.Add(new ValidationError($"{commandName}.ContractId is empty"));
        return validationErrors;
    }

    /// <summary>
    /// Validates that the TimePeriod is defined and not set to an invalid enum value.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="timePeriod">The time period to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static TErrors ValidateTimePeriod<TErrors>(this TErrors validationErrors, TimeFrameType timePeriod, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (!Enum.IsDefined(timePeriod))
            validationErrors.Add(new ValidationError($"{commandName}.TimePeriod has an invalid value"));
        return validationErrors;
    }

    /// <summary>
    /// Validates that the Timestamp is not the default value and is within reasonable bounds.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="timestamp">The timestamp to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static TErrors ValidateTimestamp<TErrors>(this TErrors validationErrors, DateTime timestamp, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (timestamp == default)
            validationErrors.Add(new ValidationError($"{commandName}.Timestamp is not set"));
        else if (timestamp.Year < 1900 || timestamp.Year > 2100)
            validationErrors.Add(new ValidationError($"{commandName}.Timestamp is out of valid range"));
        return validationErrors;
    }

    /// <summary>
    /// Validates that the FuturesPrice is a valid positive number.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="futuresPrice">The futures price to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static TErrors ValidateFuturesPrice<TErrors>(this TErrors validationErrors, double futuresPrice, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (double.IsNaN(futuresPrice))
            validationErrors.Add(new ValidationError($"{commandName}.FuturesPrice is not a number"));
        else if (double.IsInfinity(futuresPrice))
            validationErrors.Add(new ValidationError($"{commandName}.FuturesPrice is infinity"));
        else if (futuresPrice <= 0)
            validationErrors.Add(new ValidationError($"{commandName}.FuturesPrice must be greater than zero"));
        return validationErrors;
    }

    /// <summary>
    /// Validates that the VixFuturesPrice is a valid positive number.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="vixFuturesPrice">The VIX futures price to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static TErrors ValidateVixFuturesPrice<TErrors>(this TErrors validationErrors, double vixFuturesPrice, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (double.IsNaN(vixFuturesPrice))
            validationErrors.Add(new ValidationError($"{commandName}.VixFuturesPrice is not a number"));
        else if (double.IsInfinity(vixFuturesPrice))
            validationErrors.Add(new ValidationError($"{commandName}.VixFuturesPrice is infinity"));
        else if (vixFuturesPrice <= 0)
            validationErrors.Add(new ValidationError($"{commandName}.VixFuturesPrice must be greater than zero"));
        return validationErrors;
    }
}

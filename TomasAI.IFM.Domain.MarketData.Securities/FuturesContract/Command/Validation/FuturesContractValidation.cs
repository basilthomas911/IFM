using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Validation;

internal static class FuturesContractValidation
{
    /// <summary>
    /// Validates the Overwrite flag.
    /// </summary>
    /// <param name="validationErrors">A list to which validation errors will be added.</param>
    /// <param name="overwrite">The overwrite flag to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateOverwrite(this List<ValidationError> validationErrors, bool overwrite, string commandName)
    {
        // Overwrite is a boolean, so it's always valid
        // This method exists for consistency and future extensibility
        // No validation needed for boolean value type
        return validationErrors;
    }
}

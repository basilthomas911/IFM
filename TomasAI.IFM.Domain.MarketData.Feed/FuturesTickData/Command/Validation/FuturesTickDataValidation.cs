using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Validation;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Validation;

internal static class FuturesTickDataValidation
{
    /// <summary>
    /// Validates a FuturesContractV2ReadModel instance and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="contract">The futures contract to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateContract(this List<ValidationError> validationErrors, FuturesContractV2ReadModel contract, string commandName)
    {
        var validator = new FuturesContractValidationRules();
        var ruleErrors = validator.Execute(contract);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates a FuturesTickDataV2ReadModel instance and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="tickData">The tick data to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateTickData(this List<ValidationError> validationErrors, FuturesTickDataV2ReadModel tickData, string commandName)
    {
        var validator = new FuturesTickDataValidationRules();
        var ruleErrors = validator.Execute(tickData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}

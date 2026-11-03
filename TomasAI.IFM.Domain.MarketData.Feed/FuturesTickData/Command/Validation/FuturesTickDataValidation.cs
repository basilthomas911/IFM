using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Validation;

internal static class FuturesTickDataValidation
{

    /// <param name="validationErrors">
    /// The list to which validation errors will be added.
    /// </param>
    extension(List<ValidationError> validationErrors)
    {
        /// <summary>
        /// Validates a FuturesContractV3ReadModel instance and adds any validation errors to the provided list.
        /// </summary>
        ///
        /// <param name="contract">The futures contract to validate.</param>
        /// <param name="commandName">The name of the command for error reporting.</param>
        /// <returns>The updated list of validation errors.</returns>
        public List<ValidationError> ValidateContract(FuturesContractV3ReadModel contract, string commandName)
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
        ///
        /// <param name="tickData">The tick data to validate.</param>
        /// <param name="commandName">The name of the command for error reporting.</param>
        /// <returns>The updated list of validation errors.</returns>
        public List<ValidationError> ValidateTickData(FuturesTickDataV2ReadModel tickData, string commandName)
        {
            var validator = new FuturesTickDataValidationRules();
            var ruleErrors = validator.Execute(tickData);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
            return validationErrors;
        }
    }
}

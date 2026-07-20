using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Validation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Validation;

internal static class MarketDataFeedValidation
{
    /// <summary>
    /// Validates a collection of futures contracts and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">A list to which validation errors will be added.</param>
    /// <param name="futuresContracts">An array of futures contracts to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateFuturesContracts(this List<ValidationError> validationErrors, FuturesContractV2ReadModel[] futuresContracts)
    {
        if (futuresContracts is null || futuresContracts.Length == 0)
        {
            validationErrors.Add(new ValidationError("StartMarketDataFeedCommand.FuturesContracts is null or empty"));
        }
        else
        {
            var validator = new FuturesContractValidationRules();
            foreach (var contract in futuresContracts)
            {
                var ruleErrors = validator.Execute(contract);
                if (ruleErrors is not null && ruleErrors.Length > 0)
                    validationErrors.AddRange(ruleErrors);
            }
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates the ResetStream flag.
    /// </summary>
    /// <param name="validationErrors">A list to which validation errors will be added.</param>
    /// <param name="resetStream">The reset stream flag to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateResetStream(this List<ValidationError> validationErrors, bool resetStream, string commandName)
    {
        // ResetStream is a boolean, so it's always valid
        // This method exists for consistency and future extensibility
        // No validation needed for boolean value type
        return validationErrors;
    }

    /// <summary>
    /// Validates a FeedId reference type and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">A list to which validation errors will be added.</param>
    /// <param name="feedId">The feed identifier to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateFeedId(this List<ValidationError> validationErrors, FeedId feedId)
    {
        var validator = new FeedIdValidationRules();
        var ruleErrors = validator.Execute(feedId);
        if (ruleErrors is not null && ruleErrors.Length > 0)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}

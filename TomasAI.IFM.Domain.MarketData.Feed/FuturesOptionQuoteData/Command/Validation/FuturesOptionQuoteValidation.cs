using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Validation;

internal static class FuturesOptionQuoteValidation
{
    /// <summary>
    /// Validates that the QuoteId is a valid positive integer.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="quoteId">The quote ID to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateQuoteId(this List<ValidationError> validationErrors, int quoteId, string commandName)
    {
        if (quoteId <= 0)
            validationErrors.Add(new ValidationError($"{commandName}.QuoteId must be greater than zero"));
        return validationErrors;
    }

    /// <summary>
    /// Validates a collection of FuturesOptionQuoteReadModel items and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="futuresOptionQuotes">The collection of futures option quotes to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateFuturesOptionQuotes(this List<ValidationError> validationErrors, FuturesOptionQuoteReadModel[] futuresOptionQuotes, string commandName)
    {
        // Validate collection is not null
        if (futuresOptionQuotes is null)
        {
            validationErrors.Add(new ValidationError($"{commandName}.FuturesOptionQuotes collection is null"));
            return validationErrors;
        }

        // Validate collection is not empty
        if (futuresOptionQuotes.Length == 0)
        {
            validationErrors.Add(new ValidationError($"{commandName}.FuturesOptionQuotes collection is empty"));
            return validationErrors;
        }

        // Validate each item in the collection (per requirements, do NOT check for null reference value)
        var validator = new FuturesOptionQuoteValidationRules();
        foreach (var quote in futuresOptionQuotes)
        {
            var ruleErrors = validator.Execute(quote);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates a collection of FuturesOptionContractReadModel items and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="futuresOptionContracts">The collection of futures option contracts to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateFuturesOptionContracts(this List<ValidationError> validationErrors, FuturesOptionContractReadModel[] futuresOptionContracts, IReferenceLookupService refLookupService, string commandName)
    {
        // Validate collection is not null
        if (futuresOptionContracts is null)
        {
            validationErrors.Add(new ValidationError($"{commandName}.FuturesOptionContracts collection is null"));
            return validationErrors;
        }

        // Validate collection is not empty
        if (futuresOptionContracts.Length == 0)
        {
            validationErrors.Add(new ValidationError($"{commandName}.FuturesOptionContracts collection is empty"));
            return validationErrors;
        }

        // Validate each item in the collection (per requirements, do NOT check for null reference value)
        var validator = new FuturesOptionContractReadModelValidationRules(refLookupService);
        foreach (var contract in futuresOptionContracts)
        {
            var ruleErrors = validator.Execute(contract);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates that the ContractId is not null or whitespace.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="contractId">The contract ID to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateContractId(this List<ValidationError> validationErrors, string contractId, string commandName)
    {
        if (string.IsNullOrWhiteSpace(contractId))
            validationErrors.Add(new ValidationError($"{commandName}.ContractId cannot be null or empty"));
        return validationErrors;
    }

    /// <summary>
    /// Validates a QuoteData instance and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="quoteData">The quote data to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateQuoteData(this List<ValidationError> validationErrors, QuoteData quoteData, string commandName)
    {
        var validator = new QuoteDataValidationRules();
        var ruleErrors = validator.Execute(quoteData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}

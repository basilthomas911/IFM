using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Validation;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Validation;

internal static class FuturesOptionTickDataValidation
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
    /// Validates a FuturesOptionTickDataV2ReadModel instance and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="optionTickData">The option tick data to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateOptionTickData(this List<ValidationError> validationErrors, FuturesOptionTickDataV2ReadModel optionTickData, string commandName)
    {
        var validator = new FuturesOptionTickDataV2ReadModelValidationRules();
        var ruleErrors = validator.Execute(optionTickData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates a FuturesOptionContractReadModel instance and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="optionContract">The futures option contract to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateFuturesOptionContract(this List<ValidationError> validationErrors, FuturesOptionContractReadModel optionContract, IReferenceLookupService refLookupService, string commandName)
    {
        var validator = new FuturesOptionContractReadModelValidationRules(refLookupService);
        var ruleErrors = validator.Execute(optionContract);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates a FuturesContractV2ReadModel instance designated as a base contract and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="baseContract">The base futures contract to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateBaseContract(this List<ValidationError> validationErrors, FuturesContractV2ReadModel baseContract, string commandName)
    {
        var validator = new FuturesContractValidationRules();
        var ruleErrors = validator.Execute(baseContract);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates a RiskFreeRate and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="riskFreeRate">The risk-free rate to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateRiskFreeRate(this List<ValidationError> validationErrors, double riskFreeRate, string commandName)
    {
        if (double.IsNaN(riskFreeRate))
            validationErrors.Add(new ValidationError($"{commandName}.RiskFreeRate", "RiskFreeRate cannot be NaN."));
        if (double.IsInfinity(riskFreeRate))
            validationErrors.Add(new ValidationError($"{commandName}.RiskFreeRate", "RiskFreeRate cannot be Infinity."));
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
}

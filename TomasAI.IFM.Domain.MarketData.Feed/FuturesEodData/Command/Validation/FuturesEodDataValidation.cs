using System;
using System.Collections.Generic;
using TomasAI.IFM.Shared.MarketData.Validation;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Validation;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Validation;

internal static class FuturesEodDataValidation
{
    /// <summary>
    /// Validates the FuturesTickDataV2ReadModel and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="futuresTickData">The futures tick data to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateFuturesTickData(this List<ValidationError> validationErrors, FuturesTickDataV2ReadModel futuresTickData)
    {
        var validator = new FuturesTickDataValidationRules();
        var ruleErrors = validator.Execute(futuresTickData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the FuturesContractV2ReadModel and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="contract">The futures contract to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateContract(this List<ValidationError> validationErrors, FuturesContractV2ReadModel contract)
    {
        var validator = new FuturesContractValidationRules();
        var ruleErrors = validator.Execute(contract);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the FuturesEodDataV2ReadModel (today's EOD data) and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="eodDataToday">The EOD data for today to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateEodDataToday(this List<ValidationError> validationErrors, FuturesEodDataV2ReadModel eodDataToday)
    {
        var validator = new FuturesEodDataValidationRules();
        var ruleErrors = validator.Execute(eodDataToday);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the collection of FuturesEodDataV2ReadModel (historical EOD data range) and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method validates that the collection is not null or empty, and performs validation on each item in the collection.
    /// Per requirements, null reference checks are NOT performed on individual items in the collection.
    /// </remarks>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="eodDataRange">The collection of historical EOD data to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateEodDataRange(this List<ValidationError> validationErrors, ICollection<FuturesEodDataV2ReadModel> eodDataRange, string commandName)
    {
        // Validate collection is not null
        if (eodDataRange is null)
        {
            validationErrors.Add(new ValidationError($"{commandName}.EodDataRange is null"));
            return validationErrors;
        }

        // Validate collection is not empty
        if (eodDataRange.Count == 0)
        {
            validationErrors.Add(new ValidationError($"{commandName}.EodDataRange collection is empty"));
            return validationErrors;
        }

        // Validate each item in the collection (per requirements, do NOT check for null reference value)
        var validator = new FuturesEodDataValidationRules();
        foreach (var eodData in eodDataRange)
        {
            var ruleErrors = validator.Execute(eodData);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates the NormalCurveTableReadModel and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="normCurveData">The normal curve data to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateNormCurveData(this List<ValidationError> validationErrors, NormalCurveTableReadModel normCurveData)
    {
        var validator = new NormalCurveTableReadModelValidationRules();
        var ruleErrors = validator.Execute(normCurveData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the window size value and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="windowSize">The window size to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateWindowSize(this List<ValidationError> validationErrors, int windowSize, string commandName)
    {
        // Validate window size is positive
        if (windowSize <= 0)
            validationErrors.Add(new ValidationError($"{commandName}.WindowSize must be greater than zero"));

        // Validate window size is within reasonable bounds (e.g., between 1 and 1000)
        if (windowSize > 1000)
            validationErrors.Add(new ValidationError($"{commandName}.WindowSize exceeds reasonable maximum of 1000"));

        return validationErrors;
    }

    /// <summary>
    /// Validates the collection of VixFuturesEodDataReadModel and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method validates that the collection is not null or empty, and performs validation on each item in the collection.
    /// Per requirements, null reference checks are NOT performed on individual items in the collection.
    /// </remarks>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="vixEodData">The collection of VIX EOD data to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateVixEodData(this List<ValidationError> validationErrors, ICollection<VixFuturesEodDataReadModel> vixEodData, string commandName)
    {
        // Validate collection is not null
        if (vixEodData is null)
        {
            validationErrors.Add(new ValidationError($"{commandName}.VixEodData is null"));
            return validationErrors;
        }

        // Validate collection is not empty
        if (vixEodData.Count == 0)
        {
            validationErrors.Add(new ValidationError($"{commandName}.VixEodData collection is empty"));
            return validationErrors;
        }

        // Validate each item in the collection (per requirements, do NOT check for null reference value)
        var validator = new VixFuturesEodDataReadModelValidationRules();
        foreach (var vixData in vixEodData)
        {
            var ruleErrors = validator.Execute(vixData);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates the VIX FuturesTickDataV2ReadModel and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added.</param>
    /// <param name="vixFuturesTickData">The VIX futures tick data to validate.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateVixFuturesTickData(this List<ValidationError> validationErrors, FuturesTickDataV2ReadModel vixFuturesTickData)
    {
        var validator = new FuturesTickDataValidationRules();
        var ruleErrors = validator.Execute(vixFuturesTickData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}

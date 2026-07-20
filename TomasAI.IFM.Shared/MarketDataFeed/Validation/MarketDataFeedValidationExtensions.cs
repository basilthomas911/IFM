using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed.Validation;

public static class MarketDataFeedValidationExtensions
{
    /// <summary>
    /// Validates the provided futures end-of-day data and appends any validation errors to the specified list.
    /// </summary>
    /// <remarks>This method uses predefined validation rules to check the integrity and correctness of the
    /// provided futures end-of-day data. Any validation errors are appended to the <paramref name="validationErrors"/>
    /// list.</remarks>
    /// <param name="validationErrors">A list to which validation errors will be added. This list must not be null.</param>
    /// <param name="futuresEodData">The futures end-of-day data to validate. This parameter must not be null.</param>
    /// <returns>The updated list of validation errors, including any errors found during validation of the provided data.</returns>
   public static List<ValidationError> ValidateFuturesEodData(this List<ValidationError> validationErrors, FuturesEodDataV2ReadModel futuresEodData)
    {
        var ruleErrors = new FuturesEodDataValidationRules().Execute(futuresEodData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the range of futures end-of-day data and adds a validation error if the range is insufficient.
    /// </summary>
    /// <remarks>This method checks whether the <paramref name="futuresEodDataRange"/> contains at least two
    /// items. If the collection is null or contains fewer than two items, a validation error is added to the  <paramref
    /// name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">The list of validation errors to which any new errors will be added.</param>
    /// <param name="futuresEodDataRange">A collection of <see cref="FuturesEodDataV2ReadModel"/> objects representing the range of futures end-of-day
    /// data. Must contain at least two items.</param>
    /// <returns>The updated list of validation errors, including any errors added during validation.</returns>
   public static List<ValidationError> ValidateFuturesEodDataRange(this List<ValidationError> validationErrors, ICollection<FuturesEodDataV2ReadModel>? futuresEodDataRange)
    {
        if ((futuresEodDataRange?.Count ?? 0) < 2)
            validationErrors.Add(new ValidationError("InsertFuturesEodDataCommand.EodDateRange count is less than 2"));
        if (futuresEodDataRange is not null)
        {
            var validationRules = new FuturesEodDataValidationRules();
            foreach (var eodData in futuresEodDataRange)
            {
                var validationErrorsForEodData = validationRules.Execute(eodData);
                if (validationErrorsForEodData is not null)
                    validationErrors.AddRange(validationErrorsForEodData);
            }
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates the provided futures tick data against predefined validation rules and appends any validation errors
    /// to the specified list.
    /// </summary>
    /// <remarks>This method uses a set of predefined validation rules to check the integrity and correctness
    /// of the provided futures tick data. Any validation errors encountered are appended to the <paramref
    /// name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">A list to which validation errors will be added. This list must not be null.</param>
    /// <param name="futuresTickData">The futures tick data to validate. This parameter must not be null.</param>
    /// <returns>The updated list of validation errors, including any errors found during validation of the provided futures tick
    /// data.</returns>
    public static List<ValidationError> ValidateFuturesTickData(this List<ValidationError> validationErrors, FuturesTickDataV2ReadModel futuresTickData)
    {
        var ruleErrors = new FuturesTickDataValidationRules().Execute(futuresTickData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

   /// <summary>
   /// Validates the provided normal curve table and adds any validation errors to the specified list.
   /// </summary>
   /// <remarks>This method checks whether the <paramref name="normalCurveTable"/> contains valid data. If the
   /// <see cref="NormalCurveTableReadModel.NormalCurveTable"/> property is null or empty, a validation error is added
   /// to the <paramref name="validationErrors"/> list.</remarks>
   /// <param name="validationErrors">A list to which validation errors will be added. This list must not be null.</param>
   /// <param name="normalCurveTable">The normal curve table to validate. Must not be null, and its <see
   /// cref="NormalCurveTableReadModel.NormalCurveTable"/> property must contain at least one element.</param>
   /// <returns>The updated list of validation errors, including any errors related to the normal curve table validation.</returns>
    public static List<ValidationError> ValidateNormalCurveTable(this List<ValidationError> validationErrors, NormalCurveTableReadModel normalCurveTable)
    {
        if ((normalCurveTable?.NormalCurveTable?.Length ?? 0) == 0)
            validationErrors.Add(new ValidationError("InsertFuturesEodDataCommand.NormCurveData is empty"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified window size and adds a validation error to the collection if the size is invalid.
    /// </summary>
    /// <remarks>If <paramref name="windowSize"/> is less than 1, a validation error is added to the <paramref
    /// name="validationErrors"/> collection.</remarks>
    /// <param name="validationErrors">The collection of validation errors to which any new errors will be added.</param>
    /// <param name="windowSize">The window size to validate. Must be greater than or equal to 1.</param>
    /// <returns>The updated collection of validation errors, including any new errors related to the window size validation.</returns>
    public static List<ValidationError> ValidateWindowSize(this List<ValidationError> validationErrors, int windowSize)
    {
        if (windowSize < 1)
            validationErrors.Add(new ValidationError("InsertFuturesEodDataCommand.WindowSize is less than 1"));
        return validationErrors;
    }

}

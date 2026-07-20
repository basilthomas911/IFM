using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Validation;

/// <summary>
/// Provides extension methods for validating FundReadModel instances and collecting validation errors.
/// </summary>
public static class FundTransactionValidationExtensions
{
    /// <summary>
    /// Validates the FundReadModel against the FundValidationRules and adds any validation errors to the provided list of validation errors.
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateFundTransaction(this List<ValidationError> validationErrors, FundTransactionReadModel fundTransaction)
    {
        var ruleErrors = new FundTransactionValidationRules().Execute(fundTransaction);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates an array of fund transactions and adds any validation errors to the specified collection.
    /// </summary>
    /// <remarks>If the array of fund transactions is null or empty, a validation error is added. All
    /// transactions in the array must have the same FundId and OrderId; otherwise, a validation error is added and
    /// individual transactions are not validated.</remarks>
    /// <param name="validationErrors">The collection to which any validation errors will be added. Must not be null.</param>
    /// <param name="fundTransactions">An array of fund transactions to validate. All transactions must have the same FundId and OrderId.</param>
    /// <returns>The collection of validation errors, including any errors found during validation of the fund transactions.</returns>
    public static List<ValidationError> ValidateFundTransactions(this List<ValidationError> validationErrors, FundTransactionReadModel[] fundTransactions)
    {
        if (fundTransactions is null || fundTransactions.Length == 0)
            validationErrors.Add(new ValidationError($"{9999}", "ValidateFundTransactions.FundTransactions is empty"));
        else
        {
            var fundId = fundTransactions[0].FundId;
            var orderId = fundTransactions[0].OrderId;
            if (!fundTransactions.All(e => e.FundId == fundId && e.OrderId == orderId))
                validationErrors.Add(new ValidationError($"{9999}", "ValidateFundTransactions.FundTransactions must all have same FundId and OrderId"));
            else
                foreach (var fundTransaction in fundTransactions)
                    ValidateFundTransaction(validationErrors, fundTransaction);
        }
        return validationErrors;
    }
}

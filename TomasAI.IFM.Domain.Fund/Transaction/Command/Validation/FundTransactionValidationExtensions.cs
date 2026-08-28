using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Validation;

/// <summary>
/// Provides extension methods for validating FundReadModel instances and collecting validation errors.
/// </summary>
public static class FundTransactionValidationExtensions
{
    /// <summary>
    /// Validates an array of fund transactions and adds any validation errors to the specified collection.
    /// </summary>
    /// <remarks>If the array of fund transactions is null or empty, a validation error is added. All
    /// transactions in the array must have the same FundId and OrderId; otherwise, a validation error is added and
    /// individual transactions are not validated.</remarks>
    /// <param name="validationErrors">The collection to which any validation errors will be added. Must not be null.</param>
    /// <param name="fundTransactions">An array of fund transactions to validate. All transactions must have the same FundId and OrderId.</param>
    /// <returns>The collection of validation errors, including any errors found during validation of the fund transactions.</returns>
    public static List<ValidationError> ValidateFundTransactions(this List<ValidationError> validationErrors, FundTransactionReadModel[]? fundTransactions)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (fundTransactions is null || fundTransactions.Length == 0)
            validationErrors.Add(new ValidationError($"{9999}", "ValidateFundTransactions.FundTransactions is empty"));
        else
        {
            var first = fundTransactions[0];
            var fundId = first?.FundId;
            var orderId = first?.OrderId;
            for (var index = 0; index < fundTransactions.Length; index++)
            {
                var fundTransaction = fundTransactions[index];
                if (fundTransaction is not null
                    && first is not null
                    && (fundTransaction.FundId != fundId || fundTransaction.OrderId != orderId))
                {
                    validationErrors.Add(new ValidationError($"{9999}", $"FundTransactions[{index}] must have the same FundId and OrderId"));
                }
            }

            for (var index = 0; index < fundTransactions.Length; index++)
                validationErrors.ValidateFundTransaction(fundTransactions[index], $"FundTransactions[{index}]");
        }
        return validationErrors;
    }

    public static List<ValidationError> ValidateFundTransactionIdentityMatches(
        this List<ValidationError> validationErrors,
        FundTransactionEntityId? entityId,
        FundTransactionReadModel? transaction,
        string payloadName,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (entityId is { FundId: > 0, OrderId: > 0 }
            && transaction is { FundId: > 0, OrderId: > 0 }
            && (entityId.FundId != transaction.FundId || entityId.OrderId != transaction.OrderId))
            validationErrors.Add(new($"{commandName}.EntityId must match {payloadName}.FundId and OrderId"));
        return validationErrors;
    }

    public static List<ValidationError> ValidateFundTransactionsIdentityMatches(
        this List<ValidationError> validationErrors,
        FundTransactionEntityId? entityId,
        FundTransactionReadModel[]? transactions,
        string commandName)
    {
        if (transactions is null)
            return validationErrors;
        for (var index = 0; index < transactions.Length; index++)
            validationErrors.ValidateFundTransactionIdentityMatches(
                entityId,
                transactions[index],
                $"FundTransactions[{index}]",
                commandName);
        return validationErrors;
    }

    public static List<ValidationError> ValidateCorrelationId(
        this List<ValidationError> validationErrors,
        Guid correlationId,
        string commandName)
    {
        if (correlationId == Guid.Empty)
            validationErrors.Add(new($"{commandName}.CorrelationId is empty"));
        return validationErrors;
    }
}

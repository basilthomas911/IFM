using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to create multiple fund transactions, including the transactions entity id, details, and an associated error code.
/// </summary>
/// <param name="TransactionsId">The entity identifier for the batch of fund transactions.</param>
/// <param name="FundTransactions">The array of fund transaction details to be created. Cannot be null.</param>
/// <param name="CorrelationId">A unique identifier used to correlate this operation with other related operations or logs.</param>
/// <param name="ErrorCode">The error code associated with the fund transactions creation operation. Used to indicate specific error conditions or statuses.</param>
public record CreateFundTransactionsParameter(FundTransactionEntityId TransactionsId, FundTransactionReadModel[] FundTransactions, Guid CorrelationId, int ErrorCode)
    : ICommandParameter
{
}

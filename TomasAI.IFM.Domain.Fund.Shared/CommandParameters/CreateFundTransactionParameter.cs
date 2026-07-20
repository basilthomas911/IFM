using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to create a fund transaction, including the transaction details and an associated error code.
/// </summary>
/// <param name="FundTransaction">The fund transaction details to be created. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the fund transaction creation operation. Used to indicate specific error conditions or statuses.</param>
public record CreateFundTransactionParameter(FundTransactionReadModel FundTransaction, int ErrorCode)
    : ICommandParameter
{
}

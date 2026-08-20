using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to process an end-of-day fund transaction, including the transaction details and an associated error code.
/// </summary>
/// <param name="FundTransaction">The fund transaction details to process at end-of-day. Cannot be null.</param>
/// <param name="CorrelationId">The originating command identifier that must remain stable through the option-trade and fund-transaction workflow.</param>
/// <param name="ErrorCode">The error code associated with the process end-of-day fund transaction operation. Used to indicate specific error conditions or statuses.</param>
public record ProcessEndOfDayFundTransactionParameter(
    FundTransactionReadModel FundTransaction,
    Guid CorrelationId,
    int ErrorCode)
    : ICommandParameter
{
}

using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to close a fund order, including the fund order identifier and an associated error code.
/// </summary>
/// <param name="FundOrderId">The fund order identifier to be closed. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the close order operation. Used to indicate specific error conditions or statuses.</param>
public record CloseFundOrderParameter(FundOrderId FundOrderId, int ErrorCode)
    : ICommandParameter
{
}

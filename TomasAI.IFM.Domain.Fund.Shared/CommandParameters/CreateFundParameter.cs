using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to create a fund, including the fund details and an associated error code.
/// </summary>
/// <param name="Fund">The fund details to be created. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the fund creation operation. Used to indicate specific error conditions or statuses.</param>
public record CreateFundParameter(FundReadModel Fund, int ErrorCode) 
    : ICommandParameter
{
}

using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to add a futures contract.
/// </summary>
/// <param name="Contract">The futures contract details to be added. Cannot be null.</param>
/// <param name="Overwrite">True to overwrite an existing contract with the same identifier; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the add futures contract operation.</param>
public record AddFuturesContractParameter(FuturesContractV3ReadModel Contract, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}

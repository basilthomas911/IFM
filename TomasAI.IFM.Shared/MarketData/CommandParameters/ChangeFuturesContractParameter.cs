using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to change an existing futures contract.
/// </summary>
/// <param name="ContractId">The identifier of the futures contract to update. Cannot be null.</param>
/// <param name="Contract">The updated futures contract details. Cannot be null.</param>
/// <param name="Overwrite">True to overwrite the existing contract data; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the change futures contract operation.</param>
public record ChangeFuturesContractParameter(FuturesContractId ContractId, FuturesContractV2ReadModel Contract, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}

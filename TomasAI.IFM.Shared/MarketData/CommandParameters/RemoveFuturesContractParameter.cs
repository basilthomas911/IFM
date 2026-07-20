using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to remove a futures contract.
/// </summary>
/// <param name="ContractId">The identifier of the futures contract to remove. Cannot be null.</param>
/// <param name="Overwrite">True to force/overwrite removal where applicable; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the remove futures contract operation.</param>
public record RemoveFuturesContractParameter(FuturesContractId ContractId, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}

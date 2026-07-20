using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to remove a futures option contract.
/// </summary>
/// <param name="ContractId">The raw contract identifier string. Cannot be null or empty.</param>
/// <param name="Overwrite">True to force/overwrite removal where applicable; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the remove futures option contract operation.</param>
public record RemoveFuturesOptionContractParameter(string ContractId, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}

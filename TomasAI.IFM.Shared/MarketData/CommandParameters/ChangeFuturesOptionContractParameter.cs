using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to change an existing futures option contract.
/// </summary>
/// <param name="ContractId">The raw contract identifier string. Cannot be null or empty.</param>
/// <param name="Contract">The updated futures option contract details. Cannot be null.</param>
/// <param name="Overwrite">True to overwrite the existing contract data; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the change futures option contract operation.</param>
public record ChangeFuturesOptionContractParameter(string ContractId, FuturesOptionContractReadModel Contract, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}

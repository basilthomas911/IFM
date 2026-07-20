using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to add multiple futures option contracts.
/// </summary>
/// <param name="Contracts">The array of futures option contracts to be added. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the add futures option contracts operation.</param>
public record AddFuturesOptionContractsParameter(int Year,FuturesOptionContractReadModel[] Contracts, int ErrorCode)
    : ICommandParameter
{
}

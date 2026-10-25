using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to add multiple futures option contracts.
/// </summary>
/// <param name="Contracts">The array of futures option contracts to be added. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the add futures option contracts operation.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
public record AddFuturesOptionContractsParameter(int Year, FuturesOptionContractReadModel[] Contracts, int ErrorCode)
    : ICommandParameter
{
}

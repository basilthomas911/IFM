using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to add a futures option contract.
/// </summary>
/// <param name="Contract">The futures option contract details to be added. Cannot be null.</param>
/// <param name="MaturityDateYear">The year when the futures option contract matures.</param>
/// <param name="Overwrite">True to overwrite an existing contract with the same identifier; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the add futures option contract operation.</param>
public record AddFuturesOptionContractParameter(FuturesOptionContractReadModel Contract, int MaturityDateYear, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}

using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed record ResolvedCurrentFuturesContract(
    FuturesContractV3ReadModel Contract,
    DateOnly NextRolloverDate);

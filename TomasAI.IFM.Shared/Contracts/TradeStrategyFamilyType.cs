// Keep the established public namespace while sharing this classification below both domains.
namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public enum TradeStrategyFamilyType
{
    Unknown = 0, Futures = 1, FuturesOption = 2, Equity = 3,
    EquityOptions = 4, FixedIncome = 5, FixedIncomeOptions = 6
}

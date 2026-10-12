using TomasAI.IFM.Domain.MarketData.Shared;







































namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Represents a unique key for identifying a range of trading days within a specific market and currency context.
/// </summary>
/// <remarks>This record is used to encapsulate the start and end dates of a trading period, along with the
/// associated market type and currency type. It is primarily intended for scenarios where trading day ranges need to be
/// uniquely identified or compared.</remarks>
/// <param name = "StartDate"></param>
/// <param name = "EndDate"></param>
/// <param name = "MarketType"></param>
/// <param name = "CurrencyType"></param>
record TradingDaysKey(DateOnly StartDate, DateOnly EndDate, MarketType MarketType, CurrencyType CurrencyType)
{
    public override string ToString() => $"{StartDate:yyyy-MM-dd}|{EndDate:yyyy-MM-dd}|{MarketType}|{CurrencyType}";
}

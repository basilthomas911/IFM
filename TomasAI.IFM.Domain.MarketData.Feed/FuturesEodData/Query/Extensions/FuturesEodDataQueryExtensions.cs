using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;

public static class FuturesEodDataQueryExtensions
{
    /// <summary>
	/// Calculates 50-day and 200-day moving averages for a futures contract and symbol as of a given date.
	/// </summary>
	internal static async ValueTask<FuturesEodDataMovingAveragesReadModel> GetFuturesEodMovingAveragesAsync(
        this IDbContextFactory dbFactory, string contractId, string symbol, DateOnly valueDate)
    {
        var db = dbFactory.MarketDataDb;
        var fiftyDayMAqry = await db.GetFuturesEodClosingPricesAsync(contractId, symbol, valueDate.AddYears(-1), valueDate, 50);
        var fiftyDayMA = fiftyDayMAqry.Count > 0 ? fiftyDayMAqry.Average(e => e.ClosingPrice) : 0;
        var twoHundredDayMAqry = await db.GetFuturesEodClosingPricesAsync(contractId, symbol, valueDate.AddYears(-1), valueDate, 200);
        var twoHundredDayMA = twoHundredDayMAqry.Count > 0 ? twoHundredDayMAqry.Average(e => e.ClosingPrice) : 0;
        return new FuturesEodDataMovingAveragesReadModel(symbol, valueDate, fiftyDayMA, twoHundredDayMA);
    }
}

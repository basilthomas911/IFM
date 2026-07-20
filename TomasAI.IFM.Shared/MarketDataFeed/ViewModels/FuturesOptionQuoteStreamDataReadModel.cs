using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels
{
    public record FuturesOptionQuoteStreamDataReadModel(
        FeedId StreamId,
        DateTime QuoteTime,
        QuoteLevelType LevelType,
        int Position,
        int Operation,
        int Side,
        QuoteType QuoteType,
        double Price,
        double Size)
    {
    }
}

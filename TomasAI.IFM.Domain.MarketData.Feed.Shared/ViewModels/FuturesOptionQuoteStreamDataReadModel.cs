using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels
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

using System;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events
{
    public record FuturesOptionQuoteStreamingDataEvent : ServiceEvent
    {
        public int ErrorCode => 6009;

        public int QuoteId { get; init; }
        public int RequestId { get; init; }  
        public QuoteData QuoteData { get; init; }
    }
}

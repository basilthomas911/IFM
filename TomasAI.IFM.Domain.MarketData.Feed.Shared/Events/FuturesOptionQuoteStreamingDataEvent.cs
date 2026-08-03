using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events
{
    [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
    public partial record FuturesOptionQuoteStreamingDataEvent : ServiceEvent
    {
        [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
        [IgnoreMember] public const string Verb = "StreamingData";
        [IgnoreMember] public int ErrorCode => 6009;

        public int QuoteId { get; init; }
        public int RequestId { get; init; }
        public QuoteData QuoteData { get; init; }
    }
}

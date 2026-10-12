namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public enum RealtimeTickPublisherFailure
{
    None,
    Saturated,
    QueueExpired,
    TransportFailed,
    SendTimedOut,
    NonCooperativeSend
}

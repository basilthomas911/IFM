namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

public enum RealtimeTickPublisherFailure
{
    None,
    Saturated,
    QueueExpired,
    TransportFailed,
    SendTimedOut,
    NonCooperativeSend
}

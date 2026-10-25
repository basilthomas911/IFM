namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

public interface ITickAggregationPublisherDiagnostics
{
    RealtimeTickPublisherSnapshot GetSnapshot();
}

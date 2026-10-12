namespace TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

public interface ITickAggregationPublisherDiagnostics
{
    RealtimeTickPublisherSnapshot GetSnapshot();
}

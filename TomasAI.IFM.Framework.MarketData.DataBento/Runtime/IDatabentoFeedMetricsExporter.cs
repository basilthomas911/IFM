namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoFeedMetricsExporter
{
    void Export(FeedMetricsSnapshot snapshot);
}

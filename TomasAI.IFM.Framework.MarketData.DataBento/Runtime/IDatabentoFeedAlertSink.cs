namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoFeedAlertSink
{
    void Alert(FeedMetricsSnapshot snapshot);
}

using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

public interface IDatabentoLastPriceStore :
    IDatabentoLastPriceWriter,
    IDatabentoLastPriceReaderProvider,
    IDisposable
{
    DateOnly ValueDate { get; }
    int Capacity { get; }
    int Count { get; }
    bool IsActive { get; }
    void Invalidate();
}

using System.Threading.Channels;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoOperationRunner : IAsyncDisposable
{
    Task<T> RunAsync<T>(
        Func<IDatabentoMarketDataQueries, T> operation,
        CancellationToken cancellationToken = default);
}

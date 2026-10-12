using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public interface IDatabentoOptionChainSessionManager : IAsyncDisposable
{
    int ActiveSessionCount { get; }
    Task<bool> StartAsync(
        DatabentoOptionChainSessionRequest request,
        CancellationToken cancellationToken = default);
    Task<bool> StopAsync(
        string futuresContractId,
        DateOnly maturityDate);
}

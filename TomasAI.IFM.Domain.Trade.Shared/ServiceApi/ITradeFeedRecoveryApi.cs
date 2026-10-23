namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

public interface ITradeFeedRecoveryApi
{
    Task<bool> LoadAsync(TradeEntityId trade, DateOnly date, CancellationToken token = default);
    Task<bool> EnableAsync(TradeEntityId trade, DateOnly date, CancellationToken token = default);
    Task<bool> DisableAsync(TradeEntityId trade, DateOnly date, CancellationToken token = default);
}

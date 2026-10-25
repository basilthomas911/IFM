using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Recovery;

[MessagePackObject]
public sealed record TradeFeedRecoveryRegistration(
    [property: Key(0)] string TradeId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] string[] OptionContracts,
    [property: Key(3)] DateTimeOffset UpdatedAtUtc);

public interface ITradeFeedRecoveryStore
{
    Task<TradeFeedRecoveryRegistration[]> ReadAsync(DateOnly date, CancellationToken token);
    Task SaveAsync(TradeFeedRecoveryRegistration registration, CancellationToken token);
    Task RemoveAsync(string tradeId, DateOnly date, CancellationToken token);
    Task CloseAsync(DateOnly date, CancellationToken token);
}

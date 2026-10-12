using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
/// <summary>Nonblocking live observation ingress and a read of the last persisted expiry-specific IV metric.</summary>
public interface IOptionChainIvCollector
{
    void Observe(EvaluatedOptionChainReadModel chain, DateOnly valueDate);
    Task<LatestVolatilityResult> GetLatestAsync(EvaluatedOptionChainReadModel chain, CancellationToken token);
}

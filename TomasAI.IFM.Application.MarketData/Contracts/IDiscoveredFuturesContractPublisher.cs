using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Publishes a discovered futures contract through its authoritative catalog actor.</summary>
public interface IDiscoveredFuturesContractPublisher
{
    Task PublishAsync(FuturesContractV3ReadModel definition, CancellationToken token);
}

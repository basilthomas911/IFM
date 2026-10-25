using System.Collections.Immutable;
using TomasAI.IFM.Shared.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Reads saved statistics before allocating a chain; never called from a tick callback.</summary>
public interface IOptionChainStatisticsStore
{
    Task<ImmutableArray<OptionChainStatisticsSnapshot>> ReadOptionChainStatisticsAsync(
        string underlyingContractId, DateOnly expiryDate, DateOnly valueDate, CancellationToken cancellationToken);
}

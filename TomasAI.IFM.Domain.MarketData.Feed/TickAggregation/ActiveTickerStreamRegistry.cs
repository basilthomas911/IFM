using System.Collections.Concurrent;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation;

/// <summary>
/// Tracks the stream registrations and contract state owned by one event actor instance.
/// </summary>
internal sealed class ActiveTickerStreamRegistry<TContract>
{
    private readonly ConcurrentDictionary<StreamKey, TContract> _streams = [];

    /// <summary>
    /// Records the contract state for an idempotent workflow-owned stream registration.
    /// </summary>
    public void Track(
        TickerStreamOwner owner,
        string contractId,
        TContract contract)
    {
        owner.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        _streams[new StreamKey(contractId, owner)] = contract;
    }

    /// <summary>
    /// Removes the contract state owned by the specified workflow.
    /// </summary>
    public bool Untrack(
        string contractId,
        TickerStreamOwner owner) =>
        _streams.TryRemove(new StreamKey(contractId, owner), out _);

    /// <summary>
    /// Finds actor-owned contract state for the supplied contract.
    /// </summary>
    public bool TryGetContract(
        string contractId,
        out TContract contract)
    {
        foreach (var pair in _streams)
        {
            if (StringComparer.Ordinal.Equals(pair.Key.ContractId, contractId))
            {
                contract = pair.Value;
                return true;
            }
        }
        contract = default!;
        return false;
    }

    /// <summary>
    /// Removes and returns every workflow-owned registration so the actor can release them.
    /// </summary>
    public KeyValuePair<(string ContractId, TickerStreamOwner Owner), TContract>[] Drain()
    {
        var registrations = _streams
            .Select(pair => new KeyValuePair<(string, TickerStreamOwner), TContract>(
                (pair.Key.ContractId, pair.Key.Owner),
                pair.Value))
            .ToArray();
        _streams.Clear();
        return registrations;
    }

    private readonly record struct StreamKey(
        string ContractId,
        TickerStreamOwner Owner);
}

using System.Collections.Concurrent;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>
/// Retains the active legacy Start/Stop attachments that consume the shared closed-observation stream.
/// </summary>
/// <typeparam name="TEntityId">Configured indicator entity identity.</typeparam>
public static class FuturesTradeSessionBarAttachmentRegistry<TEntityId>
    where TEntityId : notnull
{
    static readonly ConcurrentDictionary<TEntityId, byte> Attachments = new();
    static readonly ConcurrentDictionary<TEntityId, (DateTimeOffset Through, bool Accepted)> Progress = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The generic attachment registry deliberately scopes static registrations by observation model type.")]
    public static void Observe(TEntityId entityId, DateTimeOffset through, bool accepted)
    {
        if (Attachments.ContainsKey(entityId)) Progress[entityId] = (through, accepted);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The generic attachment registry deliberately scopes static registrations by observation model type.")]
    public static bool HasProcessed(TEntityId entityId, DateTimeOffset through)
        => Progress.TryGetValue(entityId, out var value) && value.Accepted && value.Through >= through;

    /// <summary>Attaches an indicator identity to the shared observation stream.</summary>
    /// <param name="entityId">Indicator identity to attach.</param>
    /// <returns><see langword="true"/> when a new attachment was added.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The generic attachment registry deliberately scopes static registrations by observation model type.")]
    public static bool Attach(TEntityId entityId) => Attachments.TryAdd(entityId, 0);

    /// <summary>Detaches an indicator identity from the shared observation stream.</summary>
    /// <param name="entityId">Indicator identity to detach.</param>
    /// <returns><see langword="true"/> when an attachment was removed.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The generic attachment registry deliberately scopes static registrations by observation model type.")]
    public static bool Detach(TEntityId entityId)
    {
        Progress.TryRemove(entityId, out _);
        return Attachments.TryRemove(entityId, out _);
    }

    /// <summary>Gets a stable snapshot of the currently attached identities.</summary>
    /// <returns>The attached identities at the time of the call.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The generic attachment registry deliberately scopes static registrations by observation model type.")]
    public static TEntityId[] Snapshot() => Attachments.Keys.ToArray();

    /// <summary>Removes every attachment owned by this indicator type during shutdown.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The generic attachment registry deliberately scopes static registrations by observation model type.")]
    public static void Clear() { Attachments.Clear(); Progress.Clear(); }
}

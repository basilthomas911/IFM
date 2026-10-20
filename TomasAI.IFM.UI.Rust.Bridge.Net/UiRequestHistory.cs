namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Tracks active work separately from a bounded completion history. The owning gateway supplies synchronization.</summary>
internal sealed class UiRequestHistory<TPayload>(int capacity = 256, int shortHistoryLimit = 8)
{
    readonly Dictionary<Guid, (TPayload Payload, EditorOutcome Outcome)> pending = [];
    readonly Dictionary<Guid, Completed> completed = [];
    readonly LinkedList<Guid> completionOrder = new();
    readonly LinkedList<Guid> shortOrder = new();
    sealed record Completed(TPayload Payload, EditorOutcome Outcome, LinkedListNode<Guid> Completion,
        LinkedListNode<Guid>? Short);

    /// <summary>Gets the number of requests still executing.</summary>
    internal int PendingCount => pending.Count;
    /// <summary>Gets the bounded number of completed deduplication records.</summary>
    internal int CompletedCount => completed.Count;

    /// <summary>Finds an active or recently completed request without scanning the queue.</summary>
    internal bool TryGetValue(Guid id, out (TPayload Payload, EditorOutcome Outcome) value)
    {
        if (pending.TryGetValue(id, out value)) return true;
        if (completed.TryGetValue(id, out var found)) { value = (found.Payload, found.Outcome); return true; }
        value = default;
        return false;
    }

    /// <summary>Admits new work, evicting oldest completed records at capacity; active work is never evicted.</summary>
    internal bool TryBegin(Guid id, TPayload payload, string message)
    {
        if (pending.Count >= capacity) return false;
        while (pending.Count + completed.Count >= capacity && completionOrder.First is { } oldest)
            Evict(oldest.Value);
        pending.Add(id, (payload, new(id, "Pending", message)));
        return true;
    }

    /// <summary>Removes finished work immediately and retains only its bounded acknowledgment and duplicate identity.</summary>
    internal void Complete(Guid id, EditorOutcome outcome, bool shortLived = false)
    {
        var payload = pending[id].Payload;
        pending.Remove(id);
        completed.Add(id, new(payload, outcome, completionOrder.AddLast(id), shortLived ? shortOrder.AddLast(id) : null));
        while (shortOrder.Count > shortHistoryLimit && shortOrder.First is { } oldest)
            Evict(oldest.Value);
    }

    /// <summary>Returns completion acknowledgments in order, followed by active requests.</summary>
    internal EditorOutcome[] Outcomes()
    {
        var outcomes = new EditorOutcome[completed.Count + pending.Count];
        var index = 0;
        foreach (var id in completionOrder) outcomes[index++] = completed[id].Outcome;
        foreach (var request in pending.Values) outcomes[index++] = request.Outcome;
        return outcomes;
    }

    void Evict(Guid id)
    {
        var entry = completed[id];
        completionOrder.Remove(entry.Completion);
        if (entry.Short is { } shortEntry) shortOrder.Remove(shortEntry);
        completed.Remove(id);
    }
}

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Pure ownership and lease transition rules, independent of database persistence.</summary>
public static class DurableSubscriptionTransition
{
    /// <summary>Evaluates a validated authority mutation against the current subscription snapshot.</summary>
    /// <param name="current">The current immutable snapshot.</param>
    /// <param name="input">The validated authority mutation.</param>
    /// <param name="factDigest">The canonical authority fact digest.</param>
    /// <param name="next">The proposed snapshot, or unchanged snapshot on conflict.</param>
    /// <returns>The transition outcome to persist.</returns>
    public static DurableIntentResultCode Apply(DurableSubscriptionSnapshot current, DurableAuthorityMutation input,
        string factDigest, out DurableSubscriptionSnapshot next)
    {
        next = current;
        var source = current.Authorities.SingleOrDefault(value => string.Equals(value.SourceId,input.SourceId,StringComparison.Ordinal));
        if (source is not null && source.Owner != input.Owner
            || current.Authorities.Any(value => value.Owner == input.Owner && !string.Equals(value.SourceId,input.SourceId,StringComparison.Ordinal)))
            return DurableIntentResultCode.AuthorityConflict;
        if (source is not null && input.SourceVersion < source.SourceVersion) return DurableIntentResultCode.StaleAuthority;
        if (source is not null && input.SourceVersion == source.SourceVersion)
            return string.Equals(source.FactDigest,factDigest,StringComparison.Ordinal) ? DurableIntentResultCode.AlreadyApplied : DurableIntentResultCode.AuthorityConflict;
        // Deltas require contiguous delivery. A separately identified complete committed snapshot
        // carries the real source watermark and can safely cover intermediate non-ownership events.
        if (!input.CompleteSourceSnapshot && input.SourceVersion != (source?.SourceVersion ?? 0) + 1)
            return DurableIntentResultCode.AuthorityGap;
        if (input.ExpectedRevision != current.Revision) return DurableIntentResultCode.RevisionConflict;
        if (current.Revision == long.MaxValue) return DurableIntentResultCode.CapacityExceeded;
        var leases = source?.Leases.ToDictionary(value => value.LeaseId) ?? [];
        if (input.Status == DurableAuthorityStatus.Terminal) leases.Clear();
        else if (input.Status == DurableAuthorityStatus.Active)
        {
            if (input.CompleteSourceSnapshot)
                foreach (var id in leases.Keys.Where(id => !input.Adds.Any(x => x.LeaseId == id)).ToArray()) leases.Remove(id);
            foreach (var release in input.Releases)
            {
                if (!leases.TryGetValue(release.LeaseId, out var existing) || existing.LeaseVersion != release.ExpectedLeaseVersion)
                    return DurableIntentResultCode.LeaseConflict;
                leases.Remove(release.LeaseId);
            }
            foreach (var add in input.Adds)
            {
                if (leases.TryGetValue(add.LeaseId, out var existing))
                {
                    if (existing != add) return DurableIntentResultCode.LeaseConflict;
                    continue;
                }
                if (add.LeaseVersion != 1 || leases.Values.Any(value => value.Purpose == add.Purpose && value.Ticker == add.Ticker)
                    || current.Authorities.Where(value => !string.Equals(value.SourceId,input.SourceId,StringComparison.Ordinal))
                        .Any(value => value.Leases.Any(lease => lease.LeaseId == add.LeaseId)))
                    return DurableIntentResultCode.LeaseConflict;
                leases.Add(add.LeaseId, add);
            }
        }
        if (leases.Count > DurableSubscriptionContract.MaximumOwnerLeases
            || current.Authorities.Count(value => !string.Equals(value.SourceId,input.SourceId,StringComparison.Ordinal)) + 1 > DurableSubscriptionContract.MaximumAuthorities
            || current.Authorities.Where(value => !string.Equals(value.SourceId,input.SourceId,StringComparison.Ordinal)).Sum(value => value.Leases.Count) + leases.Count
                > DurableSubscriptionContract.MaximumLeases)
            return DurableIntentResultCode.CapacityExceeded;
        var updated = new DurableAuthorityState(input.SourceId, input.SourceVersion, input.SourceEventId, factDigest,
            input.Owner, input.Status, input.ReasonCode, leases.Values.ToArray());
        next = DurableSubscriptionContract.Freeze(current with
        {
            Revision = current.Revision + 1,
            Authorities = current.Authorities.Where(value => !string.Equals(value.SourceId,input.SourceId,StringComparison.Ordinal)).Append(updated).ToArray()
        });
        return DurableIntentResultCode.Committed;
    }
}

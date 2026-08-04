namespace TomasAI.IFM.Application.Storage;

internal static class ProjectionMutationSafety
{
    public static void ValidateStaleOperationCutoffUtc(DateTime? cutoffUtc, string parameterName)
    {
        if (cutoffUtc is not { } cutoff)
            return;
        if (cutoff.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "The stale-operation cutoff must have DateTimeKind.Utc.",
                parameterName);
        }
        if (cutoff > DateTime.UtcNow)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                cutoff,
                "The stale-operation cutoff cannot be in the future.");
        }
    }

    public static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    public static bool HasExclusiveMarker(ICollection<Guid> activeMutations, Guid mutationId)
        => activeMutations.Count == 1 && activeMutations.Contains(mutationId);

    /// <summary>
    /// A failed request may still have been applied by the database. Journals can be
    /// removed automatically only while no target mutation was submitted and any
    /// attempted ownership claim is confirmed released or absent.
    /// </summary>
    public static bool CanRemoveMutationJournalAfterFailure(
        bool targetMutationSubmissionStarted,
        bool ownershipReleaseOrAbsenceConfirmed = true,
        bool activationResponseConfirmed = true)
        => !targetMutationSubmissionStarted &&
            ownershipReleaseOrAbsenceConfirmed &&
            activationResponseConfirmed;

    /// <summary>
    /// A uniqueness reservation must outlive an ambiguously completed canonical
    /// delete or rename. Releasing it is safe only after the canonical mutation
    /// returned a positive acknowledgement.
    /// </summary>
    public static async Task ExecuteCanonicalMutationThenReleaseReservationAsync(
        Func<Task> mutateCanonicalAsync,
        Func<Task> releaseReservationAsync)
    {
        ArgumentNullException.ThrowIfNull(mutateCanonicalAsync);
        ArgumentNullException.ThrowIfNull(releaseReservationAsync);

        await mutateCanonicalAsync().ConfigureAwait(false);
        await releaseReservationAsync().ConfigureAwait(false);
    }

    public static bool CanPublishReady(
        bool operationSucceeded,
        bool ownsWriteEpoch,
        bool wasReadyOrExactlyReconciled,
        bool markerIsExclusive,
        bool generationStillMatches,
        bool ownershipReleasedWithoutConflict)
        => operationSucceeded &&
            ownsWriteEpoch &&
            wasReadyOrExactlyReconciled &&
            markerIsExclusive &&
            generationStillMatches &&
            ownershipReleasedWithoutConflict;
}

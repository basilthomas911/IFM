namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public readonly record struct DatasetWorkerAdmission(
    string Dataset,
    DateOnly ValueDate,
    Guid WorkerInstanceId,
    Guid GenerationId,
    long ManifestRevision);

/// <summary>Atomic host-side gate preventing stale worker generations from mutating current state.</summary>
public sealed class DatasetWorkerAdmissionRegistry
{
    readonly object gate = new();
    readonly Dictionary<string, AdmissionState> admissions = new(StringComparer.Ordinal);
    long rejected;

    public long RejectedPublications => Interlocked.Read(ref rejected);

    public void Admit(DatasetWorkerAdmission admission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.Dataset);
        if (admission.ValueDate == default || admission.WorkerInstanceId == Guid.Empty
            || admission.GenerationId == Guid.Empty || admission.ManifestRevision < 1)
            throw new ArgumentException("Dataset worker admission identity is invalid.", nameof(admission));
        lock (gate)
        {
            if (admissions.TryGetValue(admission.Dataset, out var previous))
            {
                // Repeating admission must not reset its publication replay fence or disconnect
                // cancellation from entries already queued in the downstream publisher.
                if (previous.Identity == admission) return;
                Retire(previous);
            }
            admissions[admission.Dataset] = new AdmissionState(admission);
        }
    }

    public void Close(string dataset, Guid expectedGeneration, Action? onClosed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        lock (gate)
        {
            if (admissions.TryGetValue(dataset, out var current)
                && current.Identity.GenerationId == expectedGeneration)
            {
                admissions.Remove(dataset);
                Retire(current);
                onClosed?.Invoke();
            }
        }
    }

    public bool TryAccept(
        DatasetWorkerAdmission identity,
        long publicationSequence) => TryAccept(identity, publicationSequence, out _);

    /// <summary>
    /// Returns the admission-lifetime token with acceptance. Publishers must retain this token on
    /// queued work: closing the generation fences items that passed ingress before the close.
    /// </summary>
    public bool TryAccept(
        DatasetWorkerAdmission identity,
        long publicationSequence,
        out CancellationToken generationCancellation)
    {
        generationCancellation = default;
        if (publicationSequence < 1)
            return Reject();
        lock (gate)
        {
            if (!admissions.TryGetValue(identity.Dataset, out var current)
                || current.Identity != identity
                || publicationSequence <= current.LastSequence)
                return Reject();
            current.LastSequence = publicationSequence;
            generationCancellation = current.Stopping.Token;
            return true;
        }
    }

    public bool TryGet(string dataset, out DatasetWorkerAdmission admission)
    {
        lock (gate)
        {
            if (admissions.TryGetValue(dataset, out var current))
            {
                admission = current.Identity;
                return true;
            }
            admission = default;
            return false;
        }
    }

    static void Retire(AdmissionState state)
    {
        // CancelAsync marks the token canceled before returning but executes registrations away
        // from this admission lock. A downstream callback must not block generation fencing.
        _ = CompleteRetirementAsync(state.Stopping, state.Stopping.CancelAsync());
    }

    static async Task CompleteRetirementAsync(CancellationTokenSource stopping, Task callbacks)
    {
        try { await callbacks.ConfigureAwait(false); }
        catch (Exception) { /* External cancellation callbacks cannot prevent containment. */ }
        finally { stopping.Dispose(); }
    }

    sealed class AdmissionState(DatasetWorkerAdmission identity)
    {
        public DatasetWorkerAdmission Identity { get; } = identity;
        public CancellationTokenSource Stopping { get; } = new();
        public long LastSequence;
    }

    bool Reject()
    {
        Interlocked.Increment(ref rejected);
        return false;
    }
}

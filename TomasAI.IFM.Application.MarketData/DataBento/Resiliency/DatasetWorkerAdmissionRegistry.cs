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
    readonly Dictionary<string, DatasetWorkerAdmission> admissions = new(StringComparer.Ordinal);
    readonly Dictionary<(string Dataset, Guid Generation), long> lastSequences = [];
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
            admissions[admission.Dataset] = admission;
            lastSequences[(admission.Dataset, admission.GenerationId)] = 0;
        }
    }

    public void Close(string dataset, Guid expectedGeneration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        lock (gate)
        {
            if (admissions.TryGetValue(dataset, out var current)
                && current.GenerationId == expectedGeneration)
                admissions.Remove(dataset);
            lastSequences.Remove((dataset, expectedGeneration));
        }
    }

    public bool TryAccept(
        DatasetWorkerAdmission identity,
        long publicationSequence)
    {
        if (publicationSequence < 1)
            return Reject();
        lock (gate)
        {
            if (!admissions.TryGetValue(identity.Dataset, out var current)
                || current != identity
                || !lastSequences.TryGetValue((identity.Dataset, identity.GenerationId), out var last)
                || publicationSequence <= last)
                return Reject();
            lastSequences[(identity.Dataset, identity.GenerationId)] = publicationSequence;
            return true;
        }
    }

    public bool TryGet(string dataset, out DatasetWorkerAdmission admission)
    {
        lock (gate) return admissions.TryGetValue(dataset, out admission);
    }

    bool Reject()
    {
        Interlocked.Increment(ref rejected);
        return false;
    }
}

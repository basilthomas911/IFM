namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Evaluates one candidate-generation snapshot using only in-process worker evidence.
/// Fresh gateway control messages qualify an idle session; market records are optional.
/// </summary>
public sealed class DatabentoLocalQualificationEvaluator
{
    readonly IReadOnlyDictionary<string, DatasetSubscriptionManifest> required;
    readonly IReadOnlyDictionary<string, Guid> generations;
    readonly bool liveTrading;
    readonly bool synthetic;
    readonly TimeSpan maximumInputAge;

    public DatabentoLocalQualificationEvaluator(
        IReadOnlyList<DatasetSubscriptionManifest> frozen,
        IReadOnlyDictionary<string, Guid> generations,
        bool liveTrading, bool synthetic, TimeSpan maximumInputAge, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(generations);
        ArgumentNullException.ThrowIfNull(time);
        if (frozen.Count is < 1 or > DatasetDesiredSubscriptionRegistry.MaximumDatasets
            || frozen.Select(item => item.Dataset).Distinct(StringComparer.Ordinal).Count() != frozen.Count
            || generations.Count != frozen.Count || maximumInputAge <= TimeSpan.Zero)
            throw new ArgumentException("Local qualification requires exact bounded candidate identities.");
        required = frozen.ToDictionary(item => item.Dataset, StringComparer.Ordinal);
        this.generations = generations;
        this.liveTrading = liveTrading;
        this.synthetic = synthetic;
        this.maximumInputAge = maximumInputAge;
    }

    /// <summary>Returns true only when every required dataset has fresh, exact local proof.</summary>
    public bool IsQualified(IReadOnlyList<DatasetWorkerProcessSnapshot> current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return current.Count == required.Count
            && current.Select(worker => worker.Dataset).Distinct(StringComparer.Ordinal).Count() == current.Count
            && current.All(IsWorkerQualified);
    }

    bool IsWorkerQualified(DatasetWorkerProcessSnapshot worker)
    {
        if (!required.TryGetValue(worker.Dataset, out var manifest)
            || !generations.TryGetValue(worker.Dataset, out var expectedGeneration)
            || worker.GenerationId != expectedGeneration
            || !worker.Running || !worker.ControlResponsive
            || worker.ManifestRevision != manifest.Revision
            || worker.ManifestFingerprint != manifest.Fingerprint
            || worker.Diagnostics is not { } native
            || native.GenerationId != worker.GenerationId
            || !native.DatabentoLocallyReady
            || native.ExpectedSubscriptions != manifest.Contracts.Count
            || native.ReceivedSubscriptions < manifest.Contracts.Count
            || !synthetic && !(
                native.HeartbeatCount > 0 && native.LastHeartbeatAgeTicks <= maximumInputAge.Ticks
                || native.ProviderMessageCount > 0 && native.LastProviderMessageAgeTicks <= maximumInputAge.Ticks))
            return false;

        if (!liveTrading)
            return native.RingUsed == 0 && native.OptionRingUsed == 0
                && native.RecordsProduced == native.RecordsConsumed
                && native.OptionRecordsProduced == native.OptionRecordsConsumed;

        // A live gateway sends heartbeats when no market records arrive. Require fresh
        // gateway evidence and a drained local queue without waiting for a trade or quote.
        return (synthetic
                || native.HeartbeatCount > 0 && native.LastHeartbeatAgeTicks <= maximumInputAge.Ticks
                || native.ProviderMessageCount > 0 && native.LastProviderMessageAgeTicks <= maximumInputAge.Ticks)
            && native.RingUsed == 0 && native.OptionRingUsed == 0
            && native.RecordsProduced == native.RecordsConsumed
            && native.OptionRecordsProduced == native.OptionRecordsConsumed;
    }

}

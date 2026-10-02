namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public enum RecoveryInfrastructureKind { Nats, Redis, PostgreSql, ScyllaDb }

public sealed record RecoveryProbeResult(string Name, RecoveryInfrastructureKind Kind,
    bool Qualified, string Detail);

public interface IRecoveryInfrastructureProbe
{
    string Name { get; }
    RecoveryInfrastructureKind Kind { get; }
    Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken);
}

public sealed record DatabentoSoftGatePolicy
{
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    public int MaximumRounds { get; init; } = 3;

    public DatabentoSoftGatePolicy Validate()
    {
        if (ProbeTimeout <= TimeSpan.Zero || ProbeTimeout >= OverallTimeout
            || OverallTimeout > TimeSpan.FromMinutes(10) || RetryDelay < TimeSpan.Zero
            || RetryDelay >= OverallTimeout
            || MaximumRounds is < 1 or > 10)
            throw new InvalidOperationException("Soft recovery probe policy must be finite.");
        return this;
    }
}

public sealed record DatabentoSoftGateResult(bool Qualified, int Rounds,
    IReadOnlyList<RecoveryProbeResult> Probes);

/// <summary>Runs independent infrastructure probes concurrently within finite round and episode budgets.</summary>
public sealed class DatabentoSoftRecoveryGate
{
    readonly IRecoveryInfrastructureProbe[] probes;
    readonly DatabentoSoftGatePolicy policy;
    readonly TimeProvider time;

    public DatabentoSoftRecoveryGate(IEnumerable<IRecoveryInfrastructureProbe> probes,
        DatabentoSoftGatePolicy policy, TimeProvider time)
    {
        this.probes = probes.ToArray();
        this.policy = policy.Validate();
        this.time = time;
        if (this.probes.Length < 4 || this.probes.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name))
            || this.probes.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != this.probes.Length
            || Enum.GetValues<RecoveryInfrastructureKind>().Except(this.probes.Select(p => p.Kind)).Any())
            throw new InvalidOperationException("Soft recovery requires unique probes for all four infrastructure families.");
    }

    public async Task<DatabentoSoftGateResult> QualifyAsync(CancellationToken cancellationToken)
    {
        using var episode = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        episode.CancelAfter(policy.OverallTimeout);
        IReadOnlyList<RecoveryProbeResult> latest = [];
        for (var round = 1; round <= policy.MaximumRounds; round++)
        {
            if (episode.IsCancellationRequested) return new(false, round - 1, latest);
            var tasks = probes.Select(probe => CheckBoundedAsync(probe, episode.Token)).ToArray();
            latest = await Task.WhenAll(tasks).ConfigureAwait(false);
            if (latest.All(item => item.Qualified)) return new(true, round, latest);
            if (round == policy.MaximumRounds) return new(false, round, latest);
            try { await Task.Delay(policy.RetryDelay, time, episode.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return new(false, round, latest); }
        }
        return new(false, policy.MaximumRounds, latest);
    }

    async Task<RecoveryProbeResult> CheckBoundedAsync(IRecoveryInfrastructureProbe probe,
        CancellationToken episodeCancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(episodeCancellation);
        deadline.CancelAfter(policy.ProbeTimeout);
        try
        {
            var task = probe.CheckAsync(deadline.Token);
            var result = await task.WaitAsync(deadline.Token).ConfigureAwait(false);
            if (result is null || result.Name != probe.Name || result.Kind != probe.Kind)
                return new(probe.Name, probe.Kind, false, "Probe identity mismatch.");
            return result with { Detail = Bound(result.Detail) };
        }
        catch (Exception exception)
        {
            return new(probe.Name, probe.Kind, false,
                exception is OperationCanceledException ? "Probe deadline expired." : Bound(exception.GetType().Name));
        }
    }

    static string Bound(string? value) => string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= 256 ? value : value[..256];
}

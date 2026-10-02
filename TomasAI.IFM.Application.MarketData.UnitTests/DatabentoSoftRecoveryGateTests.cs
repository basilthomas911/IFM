using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoSoftRecoveryGateTests
{
    [Fact]
    public async Task Probes_run_concurrently_and_all_must_qualify()
    {
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Select(kind => new Probe(kind.ToString(), kind, async _ =>
            {
                Interlocked.Increment(ref entered);
                await release.Task;
                return new(kind.ToString(), kind, true, "ready");
            })).ToArray();
        var gate = new DatabentoSoftRecoveryGate(probes,
            new() { ProbeTimeout = TimeSpan.FromSeconds(2) }, TimeProvider.System);
        var operation = gate.QualifyAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.Equal(4, Volatile.Read(ref entered));
        release.SetResult();
        var result = await operation;
        Assert.True(result.Qualified);
        Assert.Equal(1, result.Rounds);
    }

    [Fact]
    public async Task Hung_probe_exhausts_finite_rounds_without_blocking_others()
    {
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Select(kind => new Probe(kind.ToString(), kind, _ => kind == RecoveryInfrastructureKind.Nats
                ? new TaskCompletionSource<RecoveryProbeResult>().Task
                : Task.FromResult(new RecoveryProbeResult(kind.ToString(), kind, true, "ready")))).ToArray();
        var gate = new DatabentoSoftRecoveryGate(probes,
            new() { ProbeTimeout = TimeSpan.FromMilliseconds(50), OverallTimeout = TimeSpan.FromSeconds(2),
                RetryDelay = TimeSpan.Zero, MaximumRounds = 2 }, TimeProvider.System);
        var result = await gate.QualifyAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(result.Qualified);
        Assert.Equal(2, result.Rounds);
        Assert.Single(result.Probes, item => !item.Qualified);
    }

    [Fact]
    public async Task Multiple_failed_families_are_reported_without_hiding_successful_probes()
    {
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Select(kind => new Probe(kind.ToString(), kind, _ =>
                kind is RecoveryInfrastructureKind.Redis or RecoveryInfrastructureKind.ScyllaDb
                    ? Task.FromResult(new RecoveryProbeResult(kind.ToString(), kind, false, "unavailable"))
                    : Task.FromResult(new RecoveryProbeResult(kind.ToString(), kind, true, "ready"))));
        var gate = new DatabentoSoftRecoveryGate(probes,
            new() { MaximumRounds = 1 }, TimeProvider.System);

        var result = await gate.QualifyAsync(CancellationToken.None);

        Assert.False(result.Qualified);
        Assert.Equal(1, result.Rounds);
        Assert.Equal(2, result.Probes.Count(item => !item.Qualified));
        Assert.Contains(result.Probes, item => item.Kind == RecoveryInfrastructureKind.Nats && item.Qualified);
        Assert.Contains(result.Probes, item => item.Kind == RecoveryInfrastructureKind.PostgreSql && item.Qualified);
    }

    [Fact]
    public async Task Probe_exception_does_not_abort_concurrent_round()
    {
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Select(kind => new Probe(kind.ToString(), kind, _ =>
                kind == RecoveryInfrastructureKind.PostgreSql
                    ? Task.FromException<RecoveryProbeResult>(new InvalidOperationException("schema mismatch"))
                    : Task.FromResult(new RecoveryProbeResult(kind.ToString(), kind, true, "ready"))));
        var gate = new DatabentoSoftRecoveryGate(probes,
            new() { MaximumRounds = 1 }, TimeProvider.System);

        var result = await gate.QualifyAsync(CancellationToken.None);

        Assert.False(result.Qualified);
        Assert.Equal(4, result.Probes.Count);
        Assert.Equal("InvalidOperationException", Assert.Single(result.Probes, item => !item.Qualified).Detail);
    }

    [Fact]
    public async Task All_unavailable_services_exhaust_finite_round_count()
    {
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Select(kind => new Probe(kind.ToString(), kind, _ =>
                Task.FromResult(new RecoveryProbeResult(kind.ToString(), kind, false, "down"))));
        var gate = new DatabentoSoftRecoveryGate(probes,
            new() { MaximumRounds = 3, RetryDelay = TimeSpan.Zero }, TimeProvider.System);

        var result = await gate.QualifyAsync(CancellationToken.None);

        Assert.False(result.Qualified);
        Assert.Equal(3, result.Rounds);
        Assert.Equal(4, result.Probes.Count(item => !item.Qualified));
    }

    [Fact]
    public async Task Wrong_probe_identity_is_not_accepted_as_qualification()
    {
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Select(kind => new Probe(kind.ToString(), kind, _ => Task.FromResult(
                new RecoveryProbeResult("different", kind, true, "ready"))));
        var gate = new DatabentoSoftRecoveryGate(probes,
            new() { MaximumRounds = 1 }, TimeProvider.System);

        var result = await gate.QualifyAsync(CancellationToken.None);

        Assert.False(result.Qualified);
        Assert.All(result.Probes, item => Assert.False(item.Qualified));
    }

    [Fact]
    public void Missing_family_and_unbounded_retry_delay_are_rejected()
    {
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Where(kind => kind != RecoveryInfrastructureKind.ScyllaDb)
            .Select(kind => new Probe(kind.ToString(), kind, _ => Task.FromResult(
                new RecoveryProbeResult(kind.ToString(), kind, true, "ready"))));
        Assert.Throws<InvalidOperationException>(() => new DatabentoSoftRecoveryGate(probes,
            new(), TimeProvider.System));
        Assert.Throws<InvalidOperationException>(() => new DatabentoSoftGatePolicy
        {
            OverallTimeout = TimeSpan.FromSeconds(10), RetryDelay = TimeSpan.FromSeconds(10)
        }.Validate());
    }

    sealed class Probe(string name, RecoveryInfrastructureKind kind,
        Func<CancellationToken, Task<RecoveryProbeResult>> run) : IRecoveryInfrastructureProbe
    {
        public string Name => name;
        public RecoveryInfrastructureKind Kind => kind;
        public Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken) => run(cancellationToken);
    }
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Child-test-only action-boundary fault injection. All preceding actions and both failure
/// actions delegate to the production implementation; this is not a substitute recovery runtime.
/// It verifies host integration at each boundary, not every internal dependency failure mode.
/// </summary>
internal sealed class RecoveryActionFaultProbe(
    IApiDatabentoRecoveryActions inner,
    DatasetWorkerProcessRecoveryService workers,
    DatasetWorkerAdmissionRegistry admissions,
    string failedAction) : IApiDatabentoRecoveryActions
{
    internal static readonly string[] Sequence =
    [
        nameof(CaptureRecoveryInputsAsync), nameof(FenceFailedGenerationAsync),
        nameof(AbandonPreviousCandidateAsync), nameof(StopDatabentoWorkersAsync),
        nameof(StartDatabentoWorkersAsync), nameof(QualifyDatabentoAsync),
        nameof(PrepareCandidateAsync), nameof(QualifyInfrastructureAsync),
        nameof(ReconcileActorsAsync), nameof(StartPublisherAsync),
        nameof(ProveDownstreamWritesAsync), nameof(AdmitGenerationAsync)
    ];

    internal List<string> Calls { get; } = [];
    internal RecoveryActionInjectedException? InjectedException { get; private set; }
    internal bool CandidateAdmittedAtFailure { get; private set; }
    internal int CandidateCountAtFailure { get; private set; }

    async Task ExecuteAsync(string action, ApiDatabentoRecoveryContext context,
        CancellationToken token, Func<ApiDatabentoRecoveryContext, CancellationToken, Task> execute)
    {
        Calls.Add(action);
        RecordWorkers();
        if (action == failedAction)
        {
            CandidateCountAtFailure = context.Generations.Count;
            CandidateAdmittedAtFailure = context.Generations.Any(candidate =>
                admissions.TryGet(candidate.Key, out var admission) && admission.GenerationId == candidate.Value);
            InjectedException = new RecoveryActionInjectedException(
                $"Injected recovery action failure: {action}; correlation={context.Request.CorrelationId:D}");
            throw InjectedException;
        }
        await execute(context, token);
        RecordWorkers();
    }

    void RecordWorkers()
    {
        foreach (var worker in workers.Current) Console.WriteLine("OWNED_WORKER_PID=" + worker.ProcessId);
    }

    /// <inheritdoc />
    public Task CaptureRecoveryInputsAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(CaptureRecoveryInputsAsync), context, token, inner.CaptureRecoveryInputsAsync);
    /// <inheritdoc />
    public Task FenceFailedGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(FenceFailedGenerationAsync), context, token, inner.FenceFailedGenerationAsync);
    /// <inheritdoc />
    public Task AbandonPreviousCandidateAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(AbandonPreviousCandidateAsync), context, token, inner.AbandonPreviousCandidateAsync);
    /// <inheritdoc />
    public Task StopDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(StopDatabentoWorkersAsync), context, token, inner.StopDatabentoWorkersAsync);
    /// <inheritdoc />
    public Task StartDatabentoWorkersAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(StartDatabentoWorkersAsync), context, token, inner.StartDatabentoWorkersAsync);
    /// <inheritdoc />
    public Task QualifyDatabentoAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(QualifyDatabentoAsync), context, token, inner.QualifyDatabentoAsync);
    /// <inheritdoc />
    public Task PrepareCandidateAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(PrepareCandidateAsync), context, token, inner.PrepareCandidateAsync);
    /// <inheritdoc />
    public Task QualifyInfrastructureAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(QualifyInfrastructureAsync), context, token, inner.QualifyInfrastructureAsync);
    /// <inheritdoc />
    public Task ReconcileActorsAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(ReconcileActorsAsync), context, token, inner.ReconcileActorsAsync);
    /// <inheritdoc />
    public Task StartPublisherAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(StartPublisherAsync), context, token, inner.StartPublisherAsync);
    /// <inheritdoc />
    public Task ProveDownstreamWritesAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(ProveDownstreamWritesAsync), context, token, inner.ProveDownstreamWritesAsync);
    /// <inheritdoc />
    public Task AdmitGenerationAsync(ApiDatabentoRecoveryContext context, CancellationToken token) =>
        ExecuteAsync(nameof(AdmitGenerationAsync), context, token, inner.AdmitGenerationAsync);
    /// <inheritdoc />
    public Task NotifySystemConsoleAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure)
    {
        Calls.Add(nameof(NotifySystemConsoleAsync));
        return inner.NotifySystemConsoleAsync(context, failure);
    }
    /// <inheritdoc />
    public Task ShutdownApiAsync(ApiDatabentoRecoveryContext context, DatabentoHardRecoveryResult failure)
    {
        Calls.Add(nameof(ShutdownApiAsync));
        RecordWorkers();
        return inner.ShutdownApiAsync(context, failure);
    }
}

/// <summary>Identifies the intentional action fault; unrelated exceptions cannot satisfy the test.</summary>
internal sealed class RecoveryActionInjectedException(string message) : IOException(message);

/// <summary>Captures structured pipeline evidence while retaining the real host logger.</summary>
internal sealed class RecoveryActionEvidenceLogger(ILogger<ApiDatabentoRecoveryPipeline> inner)
    : ILogger<ApiDatabentoRecoveryPipeline>
{
    internal ConcurrentQueue<RecoveryActionLogEvidence> Entries { get; } = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (eventId.Id is >= 17400 and <= 17403)
        {
            var fields = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary();
            Entries.Enqueue(new(eventId.Id, (string)fields["Action"]!,
                (Guid)fields["CorrelationId"]!, exception?.ToString()));
        }
        inner.Log(logLevel, eventId, state, exception, formatter);
    }
}

internal sealed record RecoveryActionLogEvidence(int EventId, string Action, Guid CorrelationId, string? Exception);

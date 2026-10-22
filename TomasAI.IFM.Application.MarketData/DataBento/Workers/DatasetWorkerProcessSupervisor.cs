using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Owns one exact child process and its duplex local control channel.</summary>
public sealed class DatasetWorkerProcessSupervisor : IDatasetWorkerProcess
{
    readonly DatabentoStage3Options options;
    readonly Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? publicationIngress;
    readonly SemaphoreSlim commands = new(1, 1);
    readonly AsyncLocal<CancellationToken?> recoveryDeadline = new();
    readonly SemaphoreSlim healthGate = new(1, 1);
    AnonymousPipeServerStream? healthCommandPipe;
    AnonymousPipeServerStream? healthResponsePipe;
    long healthSequence;
    AnonymousPipeServerStream? commandPipe;
    AnonymousPipeServerStream? responsePipe;
    AnonymousPipeServerStream? publicationPipe;
    AnonymousPipeServerStream? retentionAckPipe;
    CancellationTokenSource? publicationStopping;
    Task? publicationReader;
    Process? process;
    WindowsJob? windowsJob;
    DatasetWorkerStartRequest? identity;
    DateTime startedOnUtc;
    long sequence;
    long responseSequence;
    string bootstrapToken = string.Empty;
    bool graceful;
    bool forced;
    bool linuxGroupEstablished;
    bool linuxGroupRetired;
    bool responsive;
    bool controlStalled;
    bool dataPlaneHealthy = true;
    DatasetWorkerControlFrame? lastFrame;
    DatasetWorkerControlFrame? healthFrame;
    DatasetWorkerProcessSnapshot? disposedSnapshot;
    int disposed;
    readonly object processOwnership = new();
    bool terminationRequested;
    Task? termination;

    public DatasetWorkerProcessSupervisor(
        DatabentoStage3Options options,
        Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? publicationIngress = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Validate();
        this.publicationIngress = publicationIngress;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003", Justification = "Preserve the existing worker exit and status subscriber delegate contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0046", Justification = "Preserve the existing worker exit and status subscriber delegate contract.")]
    public event Action<DatasetWorkerProcessSnapshot>? Exited;

    public DatasetWorkerProcessSnapshot Current => Snapshot();
    /// <summary>Broken control or health framing requires a new process and fresh pipes.</summary>
    public bool RequiresProcessReplacement => controlStalled || DatasetWorkerFrameCodec.IsFaulted(commandPipe)
        || DatasetWorkerFrameCodec.IsFaulted(responsePipe) || DatasetWorkerFrameCodec.IsFaulted(healthCommandPipe)
        || DatasetWorkerFrameCodec.IsFaulted(healthResponsePipe) || process is { HasExited: true };

    public async Task<DatasetWorkerProcessSnapshot> StartAsync(
        DatasetWorkerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (process is { HasExited: false })
            throw new InvalidOperationException("A dataset worker process is already running.");
        if (process is not null)
            throw new InvalidOperationException("A supervisor owns one process lifetime; create a new supervisor for replacement.");
        // Startup owns the control channel until the hello and manifest exchange completes.
        // Health polling must not consume WorkerHello or send a command before SupervisorHello.
        await commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await StartCoreAsync(request, cancellationToken).ConfigureAwait(false); }
        catch
        {
            if (process is { HasExited: false })
            {
                forced = true;
                await ForceTerminateAsync(process, CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
        finally { commands.Release(); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    async Task<DatasetWorkerProcessSnapshot> StartCoreAsync(
        DatasetWorkerStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (process is { HasExited: false })
            throw new InvalidOperationException("A dataset worker process is already running.");
        if (!Path.IsPathFullyQualified(request.ExecutablePath))
            throw new ArgumentException("The dataset worker executable path must be absolute.", nameof(request));
        var executable = Path.GetFullPath(request.ExecutablePath);
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            throw new FileNotFoundException("The configured dataset worker executable does not exist.", executable);
        if (request.WorkerInstanceId == Guid.Empty || request.GenerationId == Guid.Empty
            || request.ValueDate == default || string.IsNullOrWhiteSpace(request.Dataset)
            || request.ManifestRevision < 1)
            throw new ArgumentException("Dataset worker start identity is invalid.", nameof(request));
        var manifest = request.Manifest?.Validate()
            ?? throw new ArgumentException("A complete parent-owned subscription manifest is required.", nameof(request));
        if (!string.Equals(manifest.Dataset,request.Dataset,StringComparison.Ordinal) || manifest.ValueDate != request.ValueDate
            || manifest.Revision != request.ManifestRevision)
            throw new ArgumentException("The start request and subscription manifest must match.", nameof(request));

        sequence = 0;
        responseSequence = 0;
        graceful = false;
        forced = false;
        responsive = false;
        dataPlaneHealthy = true;
        lastFrame = null;

        commandPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        responsePipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        publicationPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        retentionAckPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        bootstrapToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        start.Environment["IFM_DATASET_WORKER_BOOTSTRAP"] = bootstrapToken;
        foreach (var value in request.PrefixArguments) start.ArgumentList.Add(value);
        start.ArgumentList.Add("--control-in");
        start.ArgumentList.Add(commandPipe.GetClientHandleAsString());
        start.ArgumentList.Add("--control-out");
        start.ArgumentList.Add(responsePipe.GetClientHandleAsString());
        start.ArgumentList.Add("--publication-out");
        start.ArgumentList.Add(publicationPipe.GetClientHandleAsString());
        start.ArgumentList.Add("--retention-ack-in");
        start.ArgumentList.Add(retentionAckPipe.GetClientHandleAsString());
        healthCommandPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        healthResponsePipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        start.ArgumentList.Add("--health-in");
        start.ArgumentList.Add(healthCommandPipe.GetClientHandleAsString());
        start.ArgumentList.Add("--health-out");
        start.ArgumentList.Add(healthResponsePipe.GetClientHandleAsString());
        start.ArgumentList.Add("--dataset");
        start.ArgumentList.Add(request.Dataset);
        start.ArgumentList.Add("--value-date");
        start.ArgumentList.Add(request.ValueDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--worker-id");
        start.ArgumentList.Add(request.WorkerInstanceId.ToString("D"));
        start.ArgumentList.Add("--generation-id");
        start.ArgumentList.Add(request.GenerationId.ToString("D"));

        identity = request with { ExecutablePath = executable };
        lock (processOwnership)
        {
            if (terminationRequested) throw new OperationCanceledException("Worker startup was retired.");
            process = Process.Start(start) ?? throw new InvalidOperationException("Dataset worker process did not start.");
        }
        startedOnUtc = process.StartTime.ToUniversalTime();
        if (OperatingSystem.IsWindows())
        {
            windowsJob = new WindowsJob();
            windowsJob.Assign(process);
        }
        commandPipe.DisposeLocalCopyOfClientHandle();
        responsePipe.DisposeLocalCopyOfClientHandle();
        publicationPipe.DisposeLocalCopyOfClientHandle();
        retentionAckPipe.DisposeLocalCopyOfClientHandle();
        healthCommandPipe.DisposeLocalCopyOfClientHandle();
        healthResponsePipe.DisposeLocalCopyOfClientHandle();
        publicationStopping = new CancellationTokenSource();
        publicationReader = ReadPublicationsAsync(publicationStopping.Token);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => Exited?.Invoke(Snapshot());

        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (recoveryDeadline.Value is null) handshake.CancelAfter(options.WorkerHandshakeTimeout);
        var hello = await DatasetWorkerFrameCodec.ReadAsync(responsePipe,
            options.ControlFrameMaximumBytes, handshake.Token).ConfigureAwait(false);
        ValidateResponse(hello, DatasetWorkerMessageKind.WorkerHello);
        if (OperatingSystem.IsLinux())
        {
            if (UnixNative.getpgid(process.Id) != process.Id)
                throw new InvalidOperationException("The worker did not establish its exact owned process group.");
            linuxGroupEstablished = true;
        }
        lastFrame = hello;
        await SendCoreAsync(DatasetWorkerMessageKind.SupervisorHello,
            "Supervisor accepted worker identity.", handshake.Token).ConfigureAwait(false);
        _ = await SendAndReceiveAsync(DatasetWorkerMessageKind.StartManifest,
            DatasetWorkerMessageKind.StartAccepted, cancellationToken,
            options.WorkerStartTimeout, allowGenerationChange: true, manifest: manifest, startupOwnsChannel: true)
            .ConfigureAwait(false);
        return Snapshot();
    }

    /// <summary>Runs reset and lease restoration under the caller's single bounded recovery deadline.</summary>
    /// <remarks>Only commands in this async flow inherit the budget; concurrent normal requests retain their own timeout.</remarks>
    public async Task<T> RunWithRecoveryDeadlineAsync<T>(CancellationToken deadline, Func<CancellationToken, Task<T>> operation)
    {
        if (!deadline.CanBeCanceled) throw new ArgumentException("Recovery requires a bounded caller deadline.", nameof(deadline));
        ArgumentNullException.ThrowIfNull(operation);
        deadline.ThrowIfCancellationRequested();
        var previous = recoveryDeadline.Value;
        recoveryDeadline.Value = deadline;
        try { return await operation(deadline).WaitAsync(deadline).ConfigureAwait(false); }
        finally { recoveryDeadline.Value = previous; }
    }

    public async Task<DatasetWorkerControlFrame> GetHealthAsync(CancellationToken cancellationToken = default)
        => await SendAndReceiveAsync(DatasetWorkerMessageKind.HealthSnapshot,
            DatasetWorkerMessageKind.HealthSnapshot, cancellationToken).ConfigureAwait(false);

    /// <summary>Probes the independent health pipe without waiting for pricing or subscription commands.</summary>
    public async Task<DatasetWorkerControlFrame> ProbeHealthAsync(CancellationToken cancellationToken)
    {
        await healthGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = identity ?? throw new InvalidOperationException("Worker has not started.");
            var correlation = Guid.NewGuid();
            await DatasetWorkerFrameCodec.WriteAsync(healthCommandPipe!, new()
            {
                Kind = DatasetWorkerMessageKind.HealthSnapshot, WorkerInstanceId = current.WorkerInstanceId,
                Dataset = current.Dataset, ValueDate = current.ValueDate, GenerationId = current.GenerationId,
                CorrelationId = correlation, Sequence = Interlocked.Increment(ref healthSequence), BootstrapToken = bootstrapToken,
                Detail = "Independent health probe"
            }, options.ControlFrameMaximumBytes, cancellationToken).ConfigureAwait(false);
            var response = await DatasetWorkerFrameCodec.ReadAsync(healthResponsePipe!, options.ControlFrameMaximumBytes, cancellationToken).ConfigureAwait(false);
            if (response.Kind != DatasetWorkerMessageKind.HealthSnapshot || response.CorrelationId != correlation
                || response.WorkerInstanceId != current.WorkerInstanceId || response.GenerationId != current.GenerationId
                || response.Dataset != current.Dataset || response.ValueDate != current.ValueDate || response.BootstrapToken != bootstrapToken)
                throw new InvalidDataException("Independent worker health identity does not match.");
            controlStalled = response.Detail == "Business control stalled";
            responsive = !RequiresProcessReplacement;
            var observed = response with { Healthy = response.Healthy && responsive && dataPlaneHealthy && process is { HasExited: false } };
            Volatile.Write(ref healthFrame, observed);
            return observed;
        }
        finally { healthGate.Release(); }
    }

    public async Task<DatasetWorkerControlFrame> ResetAsync(CancellationToken cancellationToken = default)
        => await ResetAsync(identity?.Manifest
            ?? throw new InvalidOperationException("The worker has no current manifest."), cancellationToken)
            .ConfigureAwait(false);

    public async Task<DatasetWorkerControlFrame> ResetAsync(
        DatasetSubscriptionManifest manifest, CancellationToken cancellationToken = default)
        => await SendAndReceiveAsync(DatasetWorkerMessageKind.CooperativeReset,
            DatasetWorkerMessageKind.ResetCompleted, cancellationToken,
            options.WorkerStartTimeout, allowGenerationChange: true, manifest: manifest).ConfigureAwait(false);

    public async Task<DatasetWorkerControlFrame> ApplyManifestAsync(
        DatasetSubscriptionManifest manifest, CancellationToken cancellationToken = default)
        => await SendAndReceiveAsync(DatasetWorkerMessageKind.ApplySubscriptionManifest,
            DatasetWorkerMessageKind.SubscriptionManifestApplied, cancellationToken,
            options.WorkerStartTimeout, allowGenerationChange: true, manifest: manifest).ConfigureAwait(false);

    public async Task<DatasetWorkerProcessSnapshot> StopAsync(CancellationToken cancellationToken = default)
    {
        if (disposedSnapshot is not null) return disposedSnapshot;
        if (process is null) return Snapshot();
        if (!process.HasExited)
        {
            try
            {
                await SendAndReceiveAsync(DatasetWorkerMessageKind.GracefulStop,
                    DatasetWorkerMessageKind.Stopped, cancellationToken,
                    options.WorkerGracefulStopTimeout).ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken)
                    .WaitAsync(options.WorkerGracefulStopTimeout, cancellationToken).ConfigureAwait(false);
                graceful = true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                               || !cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    forced = true;
                    await ForceTerminateAsync(process, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        if (OperatingSystem.IsLinux() && linuxGroupEstablished)
            await TerminateLinuxGroupAsync(process.Id, cancellationToken).ConfigureAwait(false);
        return Snapshot();
    }

    public async Task HangAsync(CancellationToken cancellationToken = default)
        => await SendAsync(DatasetWorkerMessageKind.Hang, "Injected non-cooperative worker.", cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Terminates this exact owned process without waiting for its control-command semaphore.</summary>
    public Task TerminateAsync()
    {
        lock (processOwnership)
        {
            terminationRequested = true;
            if (termination?.IsFaulted == true) termination = null;
            return termination ??= TerminateCoreAsync(process);
        }
    }

    async Task TerminateCoreAsync(Process? child)
    {
        if (child is { HasExited: false })
        {
            forced = true;
            await ForceTerminateAsync(child, CancellationToken.None).ConfigureAwait(false);
        }
        if (process is { HasExited: false })
            throw new InvalidOperationException("The owned dataset worker has not exited.");
    }

    async Task ForceTerminateAsync(Process child, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsLinux() && linuxGroupEstablished)
        {
            await TerminateLinuxGroupAsync(child.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            child.Kill(entireProcessTree: true);
        }
        await child.WaitForExitAsync(cancellationToken)
            .WaitAsync(options.WorkerForceKillTimeout, cancellationToken).ConfigureAwait(false);
    }

    async Task TerminateLinuxGroupAsync(int processGroup, CancellationToken cancellationToken)
    {
        if (linuxGroupRetired) return;
        // The leader may exit on SIGTERM while a descendant ignores it. Confirm the whole
        // authenticated group is gone, including on a graceful control/leader exit.
        if (!SignalGroup(processGroup, UnixNative.SigTerm)) { linuxGroupRetired = true; return; }
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromMilliseconds(250))
        {
            if (!SignalGroup(processGroup, 0)) { linuxGroupRetired = true; return; }
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
        forced = true;
        if (!SignalGroup(processGroup, UnixNative.SigKill)) { linuxGroupRetired = true; return; }
        started = Stopwatch.GetTimestamp();
        while (SignalGroup(processGroup, 0))
        {
            if (Stopwatch.GetElapsedTime(started) >= options.WorkerForceKillTimeout)
                throw new TimeoutException("Dataset process-group exit was not confirmed; ownership is retained.");
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
        linuxGroupRetired = true;
    }

    static bool SignalGroup(int processGroup, int signal)
    {
        if (UnixNative.kill(-processGroup, signal) == 0) return true;
        var error = Marshal.GetLastWin32Error();
        if (error == 3) return false; // ESRCH: no process in this exact group.
        throw new Win32Exception(error, "Unable to signal the owned dataset process group.");
    }

    /// <summary>Installs resolved pricing inputs and temporary ownership in the existing dataset worker.</summary>
    public async Task<Pricing.WorkerOptionChainResult> AcquireOptionChainAsync(Pricing.WorkerOptionChainRequest request, CancellationToken cancellationToken)
        => (await SendAndReceiveAsync(DatasetWorkerMessageKind.AcquireOptionChain, DatasetWorkerMessageKind.OptionChainResult,
            cancellationToken, optionChain: request).ConfigureAwait(false)).OptionChainResult!;

    public async Task<Pricing.WorkerOptionChainResult> ReleaseOptionChainAsync(Pricing.WorkerOptionChainRelease request, CancellationToken cancellationToken)
        => (await SendAndReceiveAsync(DatasetWorkerMessageKind.ReleaseOptionChain, DatasetWorkerMessageKind.OptionChainResult,
            cancellationToken, optionRelease: request).ConfigureAwait(false)).OptionChainResult!;

    /// <summary>A distinct operation prevents an older worker from acknowledging ownership as a simple release.</summary>
    public async Task<Pricing.WorkerOptionChainResult> ApplyOptionChainOwnershipAsync(Pricing.WorkerOptionChainRelease request, CancellationToken cancellationToken)
        => (await SendAndReceiveAsync(DatasetWorkerMessageKind.ApplyOptionChainOwnership, DatasetWorkerMessageKind.OptionChainResult,
            cancellationToken, optionRelease: request).ConfigureAwait(false)).OptionChainResult!;

    public async Task<Pricing.CompositionSnapshotResult> CaptureCompositionSnapshotAsync(Pricing.CompositionSnapshotRequest request, CancellationToken cancellationToken)
        => (await SendAndReceiveAsync(DatasetWorkerMessageKind.CaptureCompositionSnapshot, DatasetWorkerMessageKind.CompositionSnapshotResult,
            cancellationToken, composition: request).ConfigureAwait(false)).CompositionResult!;

    /// <summary>Executes declared market-data queries and subscription operations inside the dataset process.</summary>
    public async Task<byte[]> InvokeMarketDataApiAsync(Remote.MarketDataApiCall request, CancellationToken cancellationToken)
    {
        var reply = await SendAndReceiveAsync(DatasetWorkerMessageKind.MarketDataApiCall,
            DatasetWorkerMessageKind.MarketDataApiResult, cancellationToken, apiCall: request).ConfigureAwait(false);
        if (reply.ApiError is { } error) throw new InvalidOperationException(error);
        return reply.ApiResult ?? throw new InvalidDataException("Missing market-data result.");
    }

    async Task<DatasetWorkerControlFrame> SendAndReceiveAsync(
        DatasetWorkerMessageKind request,
        DatasetWorkerMessageKind expected,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        bool allowGenerationChange = false,
        DatasetSubscriptionManifest? manifest = null,
        Pricing.WorkerOptionChainRequest? optionChain = null,
        Pricing.WorkerOptionChainRelease? optionRelease = null,
        Pricing.CompositionSnapshotRequest? composition = null,
        bool startupOwnsChannel = false, Remote.MarketDataApiCall? apiCall = null)
    {
        manifest?.Validate();
        var recovery = recoveryDeadline.Value;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, recovery ?? CancellationToken.None);
        var commandTimeout = timeout ?? options.WorkerCommandTimeout;
        // Recovery owns the only timer for all its commands, including command-semaphore waits.
        if (recovery is null) deadline.CancelAfter(commandTimeout);
        var acquired = false;
        try
        {
            if (!startupOwnsChannel)
            {
                await commands.WaitAsync(deadline.Token).ConfigureAwait(false);
                acquired = true;
            }
            var correlationId = Guid.NewGuid();
            await SendCoreAsync(request, request.ToString(), deadline.Token, manifest, correlationId, optionChain, optionRelease, composition, apiCall)
                .ConfigureAwait(false);
            var response = await DatasetWorkerFrameCodec.ReadAsync(responsePipe!,
                options.ControlFrameMaximumBytes, deadline.Token).ConfigureAwait(false);
            ValidateResponse(response, expected, allowGenerationChange, correlationId);
            if (manifest is not null && (!response.Healthy
                || response.ManifestRevision != manifest.Revision
                || !string.Equals(response.ManifestFingerprint,manifest.Fingerprint,StringComparison.Ordinal)))
                throw new InvalidDataException("The worker did not qualify the complete requested manifest.");
            if (allowGenerationChange)
                identity = identity! with
                {
                    GenerationId = response.GenerationId,
                    Manifest = manifest ?? identity.Manifest,
                    ManifestRevision = manifest?.Revision ?? identity.ManifestRevision
                };
            lastFrame = response;
            responsive = true;
            return response;
        }
        catch (OperationCanceledException error) when (recovery is null && !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            responsive = false;
            throw new TimeoutException(
                $"Dataset worker command {request} did not receive {expected} within {commandTimeout.TotalSeconds:F3}s; "
                + $"dataset={identity?.Dataset}; PID={process?.Id}; worker={identity?.WorkerInstanceId}; "
                + $"manifestRevision={manifest?.Revision ?? identity?.ManifestRevision}.", error);
        }
        catch
        {
            responsive = false;
            throw;
        }
        finally { if (acquired) commands.Release(); }
    }

    async Task SendAsync(DatasetWorkerMessageKind kind, string detail, CancellationToken cancellationToken)
    {
        await commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await SendCoreAsync(kind, detail, cancellationToken).ConfigureAwait(false); }
        finally { commands.Release(); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The owned background worker records or returns its operation failure while keeping subsequent independent work available.")]
    async Task ReadPublicationsAsync(CancellationToken cancellationToken)
    {
        var evidence = System.Threading.Channels.Channel.CreateBounded<DatasetPublicationEnvelope>(
            new System.Threading.Channels.BoundedChannelOptions(64) { SingleReader = true, SingleWriter = true });
        var retention = RetainEvidenceAsync();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var publication = await DatasetPublicationFrameCodec.ReadAsync(
                    publicationPipe!, cancellationToken).ConfigureAwait(false);
                if (publication.Kind == DatasetPublicationKind.OptionTradeEvidence)
                {
                    if (!evidence.Writer.TryWrite(publication))
                        throw new InvalidDataException("Option evidence retention queue is full.");
                }
                else if (publicationIngress is not null)
                    await publicationIngress(publication, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
        catch (EndOfStreamException)
        {
            if (!cancellationToken.IsCancellationRequested && process is { HasExited: false })
                dataPlaneHealthy = false;
        }
        catch (IOException) when (process is null || process.HasExited) { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
        catch
        {
            dataPlaneHealthy = false;
        }
        finally
        {
            evidence.Writer.TryComplete();
            await retention.ConfigureAwait(false);
        }

        async Task RetainEvidenceAsync()
        {
            try
            {
                await foreach (var publication in evidence.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        if (publicationIngress is null) throw new InvalidOperationException("No durable publication ingress.");
                        await publicationIngress(publication, cancellationToken).ConfigureAwait(false);
                        await OptionTradeAcknowledgment.WriteAsync(retentionAckPipe!, publication.PublicationSequence,
                            publication.GenerationId, true, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        dataPlaneHealthy = false;
                        await OptionTradeAcknowledgment.WriteAsync(retentionAckPipe!, publication.PublicationSequence,
                            publication.GenerationId, false, cancellationToken).ConfigureAwait(false);
                        throw;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch { dataPlaneHealthy = false; }
        }
    }

    ValueTask SendCoreAsync(DatasetWorkerMessageKind kind, string detail, CancellationToken cancellationToken,
        DatasetSubscriptionManifest? manifest = null, Guid? correlationId = null,
        Pricing.WorkerOptionChainRequest? optionChain = null, Pricing.WorkerOptionChainRelease? optionRelease = null,
        Pricing.CompositionSnapshotRequest? composition = null, Remote.MarketDataApiCall? apiCall = null)
    {
        var current = identity ?? throw new InvalidOperationException("Dataset worker has not started.");
        return DatasetWorkerFrameCodec.WriteAsync(commandPipe!, new()
        {
            Kind = kind,
            WorkerInstanceId = current.WorkerInstanceId,
            Dataset = current.Dataset,
            ValueDate = current.ValueDate,
            GenerationId = current.GenerationId,
            CorrelationId = correlationId ?? Guid.NewGuid(),
            Sequence = Interlocked.Increment(ref sequence),
            ProcessId = Environment.ProcessId,
            Detail = detail,
            BootstrapToken = bootstrapToken,
            Manifest = manifest,
            OptionChain = optionChain,
            OptionChainRelease = optionRelease,
            CompositionRequest = composition,
            ApiCall = apiCall,
            ManifestRevision = manifest?.Revision ?? current.ManifestRevision,
            ManifestFingerprint = manifest?.Fingerprint ?? current.Manifest?.Fingerprint ?? string.Empty
        }, options.ControlFrameMaximumBytes, cancellationToken);
    }

    void ValidateResponse(
        DatasetWorkerControlFrame response,
        DatasetWorkerMessageKind expected,
        bool allowGenerationChange = false,
        Guid? correlationId = null)
    {
        var current = identity!;
        if (response.WorkerInstanceId != current.WorkerInstanceId
            || !string.Equals(response.Dataset,current.Dataset,StringComparison.Ordinal)
            || response.ValueDate != current.ValueDate
            || correlationId is { } expectedCorrelation && response.CorrelationId != expectedCorrelation
            || !allowGenerationChange && response.GenerationId != current.GenerationId
            || response.ProcessId != process!.Id
            || response.Sequence <= responseSequence
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(response.BootstrapToken),
                Encoding.ASCII.GetBytes(bootstrapToken)))
            throw new InvalidDataException("Dataset worker response identity does not match the supervised process.");
        responseSequence = response.Sequence;
        if (response.Kind != expected)
        {
            // Authenticate the frame before exposing a bounded rejection. A valid startup failure
            // must not be misreported as an identity mismatch, and credentials never enter diagnostics.
            var detail = response.Detail ?? string.Empty;
            var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
            if (!string.IsNullOrEmpty(key)) detail = detail.Replace(key, "[redacted]", StringComparison.Ordinal);
            if (detail.Length > 4096) detail = detail[..4096];
            throw new InvalidDataException($"Dataset worker returned {response.Kind}, expected {expected}: {detail}");
        }
    }

    DatasetWorkerProcessSnapshot Snapshot()
    {
        if (disposedSnapshot is { } stopped) return stopped;
        var current = identity;
        var observation = lastFrame;
        var health = Volatile.Read(ref healthFrame);
        if (health is not null && health.GenerationId == current?.GenerationId
            && health.Diagnostics?.ObservedOnUtc >= (observation?.Diagnostics?.ObservedOnUtc ?? DateTime.MinValue))
            observation = health;
        var observationMatches = current is not null && observation is not null
            && observation.GenerationId == current.GenerationId
            && observation.ManifestRevision == current.ManifestRevision
            && string.Equals(observation.ManifestFingerprint,current.Manifest?.Fingerprint,StringComparison.Ordinal);
        var child = process;
        bool running;
        int? exitCode;
        int processId;
        try
        {
            running = child is not null && !child.HasExited;
            exitCode = child is { HasExited: true } ? child.ExitCode : null;
            processId = child?.Id ?? 0;
        }
        catch (InvalidOperationException) when (disposedSnapshot is not null) { return disposedSnapshot; }
        return new()
        {
            Dataset = current?.Dataset ?? string.Empty,
            WorkerInstanceId = current?.WorkerInstanceId ?? Guid.Empty,
            GenerationId = current?.GenerationId ?? Guid.Empty,
            ProcessId = processId,
            StartedOnUtc = startedOnUtc,
            Running = running,
            Healthy = running && responsive && dataPlaneHealthy && observationMatches && observation?.Healthy == true,
            GracefulStopSucceeded = graceful,
            ForcedTermination = forced,
            ExitCode = exitCode,
            Detail = observation?.Detail ?? string.Empty,
            ManifestRevision = current?.ManifestRevision ?? 0,
            ManifestFingerprint = current?.Manifest?.Fingerprint ?? string.Empty,
            Diagnostics = observationMatches ? observation?.Diagnostics : null,
            ControlResponsive = running && responsive,
            DataPlaneHealthy = running && dataPlaneHealthy
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1849", Justification = "Immediate pipe closure interrupts blocked publication reads before the bounded shutdown wait; asynchronous pipe disposal can wait for those reads.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6966", Justification = "Immediate pipe closure interrupts blocked publication reads before the bounded shutdown wait; asynchronous pipe disposal can wait for those reads.")]
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        DatasetWorkerProcessSnapshot stopped;
        try
        {
            stopped = await StopAsync().ConfigureAwait(false);
            if (stopped.Running)
                throw new InvalidOperationException("Worker exit must be verified before releasing process ownership.");
        }
        catch
        {
            // Do not freeze a Running snapshot or discard exact process/job ownership on failed
            // containment. Keep the child queryable and permit a later bounded shutdown retry.
            Volatile.Write(ref disposed, 0);
            throw;
        }
        try
        {
            publicationStopping?.Cancel();
            publicationPipe?.Dispose();
            retentionAckPipe?.Dispose();
            if (publicationReader is not null)
            {
                try { await publicationReader.WaitAsync(options.WorkerForceKillTimeout).ConfigureAwait(false); }
                catch (OperationCanceledException) { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
                catch (TimeoutException)
                {
                    // A misbehaving host publisher cannot prevent exact worker containment.
                    // Generation admission/cancellation already fences any delayed completion.
                    _ = publicationReader.ContinueWith(task => _ = task.Exception,
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted,
                        TaskScheduler.Default);
                }
            }
        }
        finally
        {
            commandPipe?.Dispose();
            healthCommandPipe?.Dispose();
            healthResponsePipe?.Dispose();
            responsePipe?.Dispose();
            publicationPipe?.Dispose();
            retentionAckPipe?.Dispose();
            publicationStopping?.Dispose();
            disposedSnapshot = stopped;
            process?.Dispose();
            windowsJob?.Dispose();
            commands.Dispose();
        }
    }

    sealed class WindowsJob : IDisposable
    {
        const uint KillOnClose = 0x00002000;
        readonly SafeJobHandle handle;

        public WindowsJob()
        {
            handle = Native.CreateJobObject(IntPtr.Zero, null);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var limits = new ExtendedLimitInformation
            {
                BasicLimitInformation = new BasicLimitInformation { LimitFlags = KillOnClose }
            };
            var size = Marshal.SizeOf<ExtendedLimitInformation>();
            var pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(limits, pointer, false);
                if (!Native.SetInformationJobObject(handle, 9, pointer, (uint)size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }

        public void Assign(Process value)
        {
            if (!Native.AssignProcessToJobObject(handle, value.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Dispose() => handle.Dispose();

        [StructLayout(LayoutKind.Sequential)]
        struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct IoCounters
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount,
                ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct ExtendedLimitInformation
        {
            public BasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
        sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            SafeJobHandle() : base(true) { }
            protected override bool ReleaseHandle() => Native.CloseHandle(handle);
        }
        static class Native
        {
            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)]
            internal static extern SafeJobHandle CreateJobObject(IntPtr attributes, string? name);
            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetInformationJobObject(SafeJobHandle job, int kind, IntPtr value, uint length);
            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);
            [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr value);
        }
    }

    static class UnixNative
    {
        internal const int SigTerm = 15;
        internal const int SigKill = 9;
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("libc", EntryPoint = "getpgid", SetLastError = true)]
        internal static extern int getpgid(int processId);
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        internal static extern int kill(int processId, int signal);
    }
}

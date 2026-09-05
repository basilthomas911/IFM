using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatasetWorkerStartRequest
{
    public required string ExecutablePath { get; init; }
    public IReadOnlyList<string> PrefixArguments { get; init; } = [];
    public required string Dataset { get; init; }
    public required DateOnly ValueDate { get; init; }
    public required Guid WorkerInstanceId { get; init; }
    public required Guid GenerationId { get; init; }
    public long ManifestRevision { get; init; } = 1;
    public DatasetSubscriptionManifest? Manifest { get; init; }
}

public sealed record DatasetWorkerProcessSnapshot
{
    public required string Dataset { get; init; }
    public required Guid WorkerInstanceId { get; init; }
    public required Guid GenerationId { get; init; }
    public required int ProcessId { get; init; }
    public required DateTime StartedOnUtc { get; init; }
    public required bool Running { get; init; }
    public required bool Healthy { get; init; }
    public required bool GracefulStopSucceeded { get; init; }
    public required bool ForcedTermination { get; init; }
    public int? ExitCode { get; init; }
    public string Detail { get; init; } = string.Empty;
    public long ManifestRevision { get; init; }
    public string ManifestFingerprint { get; init; } = string.Empty;
    public DatasetWorkerDiagnostics? Diagnostics { get; init; }
    public bool ControlResponsive { get; init; }
    public bool DataPlaneHealthy { get; init; }
}

/// <summary>Owns one exact child process and its duplex local control channel.</summary>
public sealed class DatasetWorkerProcessSupervisor : IAsyncDisposable
{
    readonly DatabentoStage3Options options;
    readonly Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? publicationIngress;
    readonly SemaphoreSlim commands = new(1, 1);
    AnonymousPipeServerStream? commandPipe;
    AnonymousPipeServerStream? responsePipe;
    AnonymousPipeServerStream? publicationPipe;
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
    bool dataPlaneHealthy = true;
    DatasetWorkerControlFrame? lastFrame;
    DatasetWorkerProcessSnapshot? disposedSnapshot;
    int disposed;

    public DatasetWorkerProcessSupervisor(
        DatabentoStage3Options options,
        Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? publicationIngress = null)
    {
        this.options = options.Validate();
        this.publicationIngress = publicationIngress;
    }

    public event Action<DatasetWorkerProcessSnapshot>? Exited;

    public DatasetWorkerProcessSnapshot Current => Snapshot();

    public async Task<DatasetWorkerProcessSnapshot> StartAsync(
        DatasetWorkerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (process is { HasExited: false })
            throw new InvalidOperationException("A dataset worker process is already running.");
        if (process is not null)
            throw new InvalidOperationException("A supervisor owns one process lifetime; create a new supervisor for replacement.");
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
    }

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
        if (manifest.Dataset != request.Dataset || manifest.ValueDate != request.ValueDate
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
        start.ArgumentList.Add("--dataset");
        start.ArgumentList.Add(request.Dataset);
        start.ArgumentList.Add("--value-date");
        start.ArgumentList.Add(request.ValueDate.ToString("yyyy-MM-dd"));
        start.ArgumentList.Add("--worker-id");
        start.ArgumentList.Add(request.WorkerInstanceId.ToString("D"));
        start.ArgumentList.Add("--generation-id");
        start.ArgumentList.Add(request.GenerationId.ToString("D"));

        identity = request with { ExecutablePath = executable };
        process = Process.Start(start) ?? throw new InvalidOperationException("Dataset worker process did not start.");
        startedOnUtc = process.StartTime.ToUniversalTime();
        if (OperatingSystem.IsWindows())
        {
            windowsJob = new WindowsJob();
            windowsJob.Assign(process);
        }
        commandPipe.DisposeLocalCopyOfClientHandle();
        responsePipe.DisposeLocalCopyOfClientHandle();
        publicationPipe.DisposeLocalCopyOfClientHandle();
        publicationStopping = new CancellationTokenSource();
        publicationReader = ReadPublicationsAsync(publicationStopping.Token);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => Exited?.Invoke(Snapshot());

        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshake.CancelAfter(options.WorkerHandshakeTimeout);
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
        await SendAsync(DatasetWorkerMessageKind.SupervisorHello,
            "Supervisor accepted worker identity.", handshake.Token).ConfigureAwait(false);
        _ = await SendAndReceiveAsync(DatasetWorkerMessageKind.StartManifest,
            DatasetWorkerMessageKind.StartAccepted, cancellationToken,
            options.WorkerStartTimeout, allowGenerationChange: true, manifest: manifest)
            .ConfigureAwait(false);
        return Snapshot();
    }

    public async Task<DatasetWorkerControlFrame> GetHealthAsync(CancellationToken cancellationToken = default)
        => await SendAndReceiveAsync(DatasetWorkerMessageKind.HealthSnapshot,
            DatasetWorkerMessageKind.HealthSnapshot, cancellationToken).ConfigureAwait(false);

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

    async Task<DatasetWorkerControlFrame> SendAndReceiveAsync(
        DatasetWorkerMessageKind request,
        DatasetWorkerMessageKind expected,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        bool allowGenerationChange = false,
        DatasetSubscriptionManifest? manifest = null)
    {
        manifest?.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? options.WorkerCommandTimeout);
        var acquired = false;
        try
        {
            await commands.WaitAsync(deadline.Token).ConfigureAwait(false);
            acquired = true;
            var correlationId = Guid.NewGuid();
            await SendCoreAsync(request, request.ToString(), deadline.Token, manifest, correlationId)
                .ConfigureAwait(false);
            var response = await DatasetWorkerFrameCodec.ReadAsync(responsePipe!,
                options.ControlFrameMaximumBytes, deadline.Token).ConfigureAwait(false);
            ValidateResponse(response, expected, allowGenerationChange, correlationId);
            if (manifest is not null && (!response.Healthy
                || response.ManifestRevision != manifest.Revision
                || response.ManifestFingerprint != manifest.Fingerprint))
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

    async Task ReadPublicationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var publication = await DatasetPublicationFrameCodec.ReadAsync(
                    publicationPipe!, cancellationToken).ConfigureAwait(false);
                if (publicationIngress is not null)
                    await publicationIngress(publication, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (EndOfStreamException)
        {
            if (!cancellationToken.IsCancellationRequested && process is { HasExited: false })
                dataPlaneHealthy = false;
        }
        catch (IOException) when (process is null || process.HasExited) { }
        catch
        {
            dataPlaneHealthy = false;
        }
    }

    ValueTask SendCoreAsync(DatasetWorkerMessageKind kind, string detail, CancellationToken cancellationToken,
        DatasetSubscriptionManifest? manifest = null, Guid? correlationId = null)
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
        if (response.Kind != expected
            || response.WorkerInstanceId != current.WorkerInstanceId
            || response.Dataset != current.Dataset
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
    }

    DatasetWorkerProcessSnapshot Snapshot()
    {
        if (disposedSnapshot is { } stopped) return stopped;
        var current = identity;
        var observation = lastFrame;
        var observationMatches = current is not null && observation is not null
            && observation.GenerationId == current.GenerationId
            && observation.ManifestRevision == current.ManifestRevision
            && observation.ManifestFingerprint == current.Manifest?.Fingerprint;
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
            if (publicationReader is not null)
            {
                try { await publicationReader.WaitAsync(options.WorkerForceKillTimeout).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
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
            responsePipe?.Dispose();
            publicationPipe?.Dispose();
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

        [StructLayout(LayoutKind.Sequential)] struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] struct IoCounters
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount,
                ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)] struct ExtendedLimitInformation
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
            [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)]
            internal static extern SafeJobHandle CreateJobObject(IntPtr attributes, string? name);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetInformationJobObject(SafeJobHandle job, int kind, IntPtr value, uint length);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr value);
        }
    }

    static class UnixNative
    {
        internal const int SigTerm = 15;
        internal const int SigKill = 9;
        [DllImport("libc", EntryPoint = "getpgid", SetLastError = true)]
        internal static extern int getpgid(int processId);
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        internal static extern int kill(int processId, int signal);
    }
}

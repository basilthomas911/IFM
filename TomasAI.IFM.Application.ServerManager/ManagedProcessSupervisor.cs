using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class ManagedProcessSupervisor : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly object _notificationSync = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly IReadOnlyList<ManagedProcessDefinition> _definitions;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Action<ManagedProcessLogEntry> _writeLog;
    private readonly Action<IReadOnlyCollection<ManagedProcessIdentity>>? _runningProcessesChanged;
    private readonly WindowsKillOnCloseJob? _developmentJob;
    private readonly Dictionary<string, OwnedProcess> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> _lastCompletions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ManagedProcessIdentity> _lastIdentities = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ManagedProcessSupervisor(
        IEnumerable<ManagedProcessDefinition> definitions,
        TimeSpan shutdownTimeout,
        Action<ManagedProcessLogEntry> writeLog,
        bool useDevelopmentKillOnCloseJob = false,
        Action<IReadOnlyCollection<ManagedProcessIdentity>>? runningProcessesChanged = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(writeLog);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(shutdownTimeout, TimeSpan.Zero);

        _definitions = definitions.Where(definition => definition.Enabled).OrderBy(definition => definition.StartOrder).ToArray();
        _shutdownTimeout = shutdownTimeout;
        _writeLog = writeLog;
        _runningProcessesChanged = runningProcessesChanged;
        _developmentJob = useDevelopmentKillOnCloseJob ? new WindowsKillOnCloseJob() : null;
    }

    public IReadOnlyCollection<string> RunningProcessKeys
    {
        get
        {
            lock (_sync)
            {
                return _running.Keys.ToArray();
            }
        }
    }

    public IReadOnlyCollection<ManagedProcessIdentity> RunningProcesses
    {
        get
        {
            lock (_sync)
            {
                return _running.Values
                    .Select(value => value.Identity)
                    .ToArray();
            }
        }
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                foreach (var definition in _definitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await StartCoreAsync(definition).ConfigureAwait(false);
                    await WaitForReadinessAsync(definition, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                await StopAllCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task RestartAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopAllCoreAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var definition in _definitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await StartCoreAsync(definition).ConfigureAwait(false);
                    await WaitForReadinessAsync(definition, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                await StopAllCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>Emergency API replacement. No graceful drain and no process-tree termination.</summary>
    public async Task<int> RestartApiAsync(int expectedProcessId, DateTime expectedStartedUtc, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            OwnedProcess? owned;
            ManagedProcessDefinition definition;
            lock (_sync)
            {
                _running.TryGetValue("api", out owned);
                var identity = owned?.Identity ?? _lastIdentities.GetValueOrDefault("api");
                if (identity is null || identity.ProcessId != expectedProcessId || identity.StartedAtUtc.UtcDateTime != expectedStartedUtc)
                    throw new InvalidOperationException("API recovery request does not identify the currently owned API process.");
                definition = owned?.Definition ?? _definitions.Single(item => item.Key.Equals("api",StringComparison.OrdinalIgnoreCase));
            }
            Process? target;
            try { target = Process.GetProcessById(expectedProcessId); }
            catch (ArgumentException) { target = null; }
            if (target is not null)
            {
                using (target)
                {
                    if (target.StartTime.ToUniversalTime() != expectedStartedUtc) throw new InvalidOperationException("API PID was reused; no process was stopped.");
                    if (!HasExited(target)) target.Kill(entireProcessTree: false);
                    await target.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            // Output pipes may be inherited or stuck. Physical process exit is sufficient;
            // MonitorAsync owns their eventual cleanup and cannot remove the replacement.
            lock (_sync)
                if (_running.TryGetValue("api", out var current) && ReferenceEquals(current, owned)) _running.Remove("api");
            cancellationToken.ThrowIfCancellationRequested();
            await StartCoreAsync(definition).ConfigureAwait(false);
            await WaitForReadinessAsync(definition, cancellationToken, inheritDeadline: true).ConfigureAwait(false);
            lock (_sync) return _running["api"].Identity.ProcessId;
        }
        finally { _lifecycle.Release(); }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopAllCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task WaitForExitAsync(string processKey, CancellationToken cancellationToken = default)
    {
        Task completion;
        lock (_sync)
        {
            if (!_lastCompletions.TryGetValue(processKey, out completion!))
            {
                throw new InvalidOperationException($"Process '{processKey}' has not been started.");
            }
        }

        await completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await StopAllAsync().ConfigureAwait(false);
        }
        finally
        {
            _developmentJob?.Dispose();
            _disposed = true;
            _lifecycle.Dispose();
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="Ownership transfers to the tracked process; monitor and startup failure paths dispose it.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1849",Justification="Synchronous process-start failure cleanup must finish before returning the failure task.")]

    private Task StartCoreAsync(ManagedProcessDefinition definition)
    {
        lock (_sync)
        {
            if (_running.ContainsKey(definition.Key))
            {
                WriteLifecycle(definition, "Start skipped because the process is already running.");
                return Task.CompletedTask;
            }
        }

        var process = new Process
        {
            StartInfo = CreateStartInfo(definition),
            EnableRaisingEvents = true
        };

        try
        {
            var workingDirectory = definition.ResolveWorkingDirectory();
            if (!Directory.Exists(workingDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Working directory '{workingDirectory}' does not exist.");
            }

            var executablePath = definition.ResolveExecutablePath();
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("Managed executable was not found.", executablePath);
            }

            if (!process.Start())
            {
                throw new InvalidOperationException("Process.Start returned false.");
            }

            _developmentJob?.Assign(process);

            var owned = new OwnedProcess(
                definition,
                process,
                new ManagedProcessIdentity(
                    definition.Key,
                    process.Id,
                    new DateTimeOffset(process.StartTime.ToUniversalTime()),
                    definition.ResolveExecutablePath(),
                    definition.StartOrder));
            lock (_sync)
            {
                _running.Add(definition.Key, owned);
                _lastIdentities[definition.Key] = owned.Identity;
                owned.Completion = MonitorAsync(owned);
                _lastCompletions[definition.Key] = owned.Completion;
            }
        }
        catch (Exception exception)
        {
            try
            {
                if (process.Id != 0 && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
                // The process did not start or exited during startup cleanup.
            }

            process.Dispose();
            WriteLifecycle(definition, $"Start failed: {exception.Message}", exception);
            lock (_sync)
            {
                _lastCompletions[definition.Key] = Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        NotifyRunningProcessesChanged();

        WriteLifecycle(definition, $"Started process {process.Id}.");

        return Task.CompletedTask;
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822",Justification="Preserve the instance contract consumed by existing callers or BenchmarkDotNet reflection.")]

    private ProcessStartInfo CreateStartInfo(ManagedProcessDefinition definition)
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = definition.WindowStyle == ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            WindowStyle = definition.WindowStyle,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = definition.ShutdownMode == ProcessShutdownMode.StandardInput,
            FileName = definition.ResolveExecutablePath(),
            WorkingDirectory = definition.ResolveWorkingDirectory()
        };

        foreach (var argument in definition.Arguments)
        {
            startInfo.ArgumentList.Add(Environment.ExpandEnvironmentVariables(argument));
        }

        foreach (var variable in definition.EnvironmentVariables)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return startInfo;
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]

    private async Task MonitorAsync(OwnedProcess owned)
    {
        var stdout = PumpAsync(owned, owned.Process.StandardOutput, ManagedProcessLogStream.StandardOutput);
        var stderr = PumpAsync(owned, owned.Process.StandardError, ManagedProcessLogStream.StandardError);

        try
        {
            await owned.Process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            WriteLifecycle(owned.Definition, $"Exited with code {owned.Process.ExitCode}.");
        }
        catch (Exception exception)
        {
            WriteLifecycle(owned.Definition, "Process monitoring failed.", exception);
        }
        finally
        {
            lock (_sync)
            {
                if (_running.TryGetValue(owned.Definition.Key, out var current) && ReferenceEquals(current, owned))
                {
                    _running.Remove(owned.Definition.Key);
                }
            }

            NotifyRunningProcessesChanged();

            owned.Process.Dispose();
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]

    private async Task PumpAsync(OwnedProcess owned, StreamReader reader, ManagedProcessLogStream stream)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                Write(owned.Definition, stream, line);
            }
        }
        catch (Exception exception)
        {
            WriteLifecycle(owned.Definition, $"{stream} capture failed.", exception);
        }
    }

    private async Task WaitForReadinessAsync(
        ManagedProcessDefinition definition,
        CancellationToken cancellationToken, bool inheritDeadline = false)
    {
        if (!string.IsNullOrWhiteSpace(definition.ReadinessPipeName))
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (!inheritDeadline) deadline.CancelAfter(TimeSpan.FromSeconds(definition.ReadinessTimeoutSeconds));
            var dashboardClient = new SchedulerPipeClient(new SchedulerClientOptions { PipeName = definition.ReadinessPipeName, ConnectTimeoutMilliseconds = 1000 });
            try
            {
                while (true)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    if (!IsRunning(definition.Key)) throw new InvalidOperationException($"Process '{definition.Key}' exited before scheduler readiness.");
                    try
                    {
                        var dashboard = await dashboardClient.GetDashboardAsync(deadline.Token).ConfigureAwait(false);
                        if (dashboard.Health.DatabaseAvailable && dashboard.Health.QuartzAvailable && dashboard.Health.SchedulingStarted)
                        {
                            WriteLifecycle(definition, "Scheduler readiness confirmed: database, Quartz and scheduling are available.");
                            return;
                        }
                    }
                    catch (IOException) { }
                    await Task.Delay(definition.ReadinessPollIntervalMilliseconds, deadline.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Process '{definition.Key}' did not become ready within {definition.ReadinessTimeoutSeconds} seconds.");
            }
        }

        if (string.IsNullOrWhiteSpace(definition.ReadinessUri))
        {
            return;
        }

        WriteLifecycle(definition, $"Waiting for readiness at {definition.ReadinessUri}.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!inheritDeadline) timeout.CancelAfter(TimeSpan.FromSeconds(definition.ReadinessTimeoutSeconds));
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        HttpStatusCode? lastStatus = null;
        Exception? lastException = null;
        try
        {
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (!IsRunning(definition.Key))
                {
                    throw new InvalidOperationException(
                        $"Process '{definition.Key}' exited before its readiness endpoint became healthy.");
                }

                try
                {
                    using var response = await client.GetAsync(new Uri(definition.ReadinessUri), timeout.Token)
                        .ConfigureAwait(false);
                    lastStatus = response.StatusCode;
                    lastException = null;
                    if (response.IsSuccessStatusCode)
                    {
                        WriteLifecycle(definition, $"Readiness confirmed with HTTP {(int)response.StatusCode}.");
                        return;
                    }
                }
                catch (HttpRequestException exception)
                {
                    lastException = exception;
                }

                await Task.Delay(definition.ReadinessPollIntervalMilliseconds, timeout.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var detail = lastStatus is not null
                ? $"last HTTP status was {(int)lastStatus.Value}"
                : lastException is not null
                    ? lastException.Message
                    : "no response was received";
            var message = $"Readiness timed out after {definition.ReadinessTimeoutSeconds} seconds: {detail}.";
            WriteLifecycle(definition, message);
            throw new TimeoutException(message);
        }
    }

    private bool IsRunning(string processKey)
    {
        lock (_sync)
        {
            return _running.ContainsKey(processKey);
        }
    }

    private async Task StopAllCoreAsync(CancellationToken cancellationToken)
    {
        OwnedProcess[] processes;
        lock (_sync)
        {
            processes = _running.Values.OrderByDescending(value => value.Definition.StartOrder).ToArray();
        }

        foreach (var owned in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await StopCoreAsync(owned, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StopCoreAsync(OwnedProcess owned, CancellationToken cancellationToken)
    {
        if (HasExited(owned.Process))
        {
            await owned.Completion.ConfigureAwait(false);
            return;
        }

        var gracefulRequested = TryRequestGracefulShutdown(owned);
        if (gracefulRequested)
        {
            WriteLifecycle(owned.Definition, "Graceful shutdown requested.");
            try
            {
                await owned.Process.WaitForExitAsync(cancellationToken)
                    .WaitAsync(_shutdownTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                WriteLifecycle(
                    owned.Definition,
                    $"Graceful shutdown exceeded {_shutdownTimeout.TotalSeconds:0.###} seconds; forcing process-tree termination.");
            }
        }
        else
        {
            WriteLifecycle(owned.Definition, "No graceful shutdown channel was available; forcing process-tree termination.");
        }

        if (!HasExited(owned.Process))
        {
            try
            {
                owned.Process.Kill(entireProcessTree: true);
                WriteLifecycle(owned.Definition, "Forced process-tree termination requested.");
            }
            catch (InvalidOperationException)
            {
                // The process exited between the state check and Kill.
            }
        }

        await owned.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]

    private bool TryRequestGracefulShutdown(OwnedProcess owned)
    {
        try
        {
            return owned.Definition.ShutdownMode switch
            {
                ProcessShutdownMode.CloseMainWindow => owned.Process.CloseMainWindow(),
                ProcessShutdownMode.StandardInput => WriteShutdownInput(owned),
                _ => false
            };
        }
        catch (Exception exception)
        {
            WriteLifecycle(owned.Definition, "Graceful shutdown request failed.", exception);
            return false;
        }
    }

    private static bool WriteShutdownInput(OwnedProcess owned)
    {
        owned.Process.StandardInput.WriteLine(owned.Definition.ShutdownInput);
        owned.Process.StandardInput.Flush();
        return true;
    }

    private void WriteLifecycle(
        ManagedProcessDefinition definition,
        string message,
        Exception? exception = null)
        => Write(definition, ManagedProcessLogStream.Lifecycle, message, exception);

    private void Write(
        ManagedProcessDefinition definition,
        ManagedProcessLogStream stream,
        string message,
        Exception? exception = null)
        => _writeLog(new ManagedProcessLogEntry(
            DateTimeOffset.Now,
            definition.Key,
            definition.DisplayName,
            stream,
            message,
            exception));
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1031",Justification="This process/UI boundary reports failures through its existing logging or error result while completing cleanup.")]

    private void NotifyRunningProcessesChanged()
    {
        if (_runningProcessesChanged is null)
        {
            return;
        }

        lock (_notificationSync)
        {
            try
            {
                _runningProcessesChanged(RunningProcesses);
            }
            catch (Exception exception)
            {
                _writeLog(new ManagedProcessLogEntry(
                    DateTimeOffset.Now,
                    "manager",
                    "Server Manager",
                    ManagedProcessLogStream.Manager,
                    "Development process-session record update failed.",
                    exception));
            }
        }
    }

    private sealed class OwnedProcess(
        ManagedProcessDefinition definition,
        Process process,
        ManagedProcessIdentity identity)
    {
        public ManagedProcessDefinition Definition { get; } = definition;

        public Process Process { get; } = process;

        public ManagedProcessIdentity Identity { get; } = identity;

        public Task Completion { get; set; } = Task.CompletedTask;
    }
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed record ManagedProcessIdentity(
    string ProcessKey,
    int ProcessId,
    DateTimeOffset StartedAtUtc,
    string ExecutablePath,
    int StartOrder);

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.ServerManager;

public sealed partial class ManagedProcessSupervisor
{
    string? captureLease;
    TaskCompletionSource? captureReleased;
    ManagedProcessIdentity? capturedScheduler;
    readonly List<ManagedProcessIdentity> capturedWriters = [];
    static readonly TimeSpan CaptureGracefulBudget = TimeSpan.FromMinutes(2);

    /// <summary>Current user requested capture holds have no effect unless explicitly acquired.</summary>
    public CaptureLifecycleReceipt CaptureStatus()
    {
        lock (_sync) return new(captureLease is null ? "Open" : "Held", captureLease, false,
            capturedScheduler, RunningProcesses, capturedWriters.ToArray());
    }

    /// <summary>Fences automatic recovery and gracefully stops UI and scheduler writers. Never kills a process.</summary>
    public async Task<CaptureLifecycleReceipt> HoldCaptureAsync(string lease, CancellationToken token)
    {
        if (lease.Length != 32 || !Guid.TryParseExact(lease, "N", out _)) throw new ArgumentException("Capture lease must be an exact GUID.", nameof(lease));
        if (!await _lifecycle.WaitAsync(TimeSpan.FromSeconds(10), token).ConfigureAwait(false))
            throw new TimeoutException("Capture cannot acquire lifecycle ownership while Development startup or recovery is incomplete; no capture hold was acquired.");
        try
        {
            OwnedProcess[] writers;
            lock (_sync)
            {
                if (captureLease is not null && captureLease != lease) throw new InvalidOperationException("Another capture owner holds lifecycle recovery.");
                captureLease = lease; captureReleased ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                writers = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(_running.Values,
                    value => value.Definition.Key is "scheduler" or "ui"));
                foreach (var writer in writers)
                    if (!capturedWriters.Contains(writer.Identity)) capturedWriters.Add(writer.Identity);
                if (_running.TryGetValue("scheduler", out var scheduler)) capturedScheduler ??= scheduler.Identity;
            }
            foreach (var writer in writers)
            {
                if (HasExited(writer.Process)) continue;
                if (!TryRequestGracefulShutdown(writer)) throw new InvalidOperationException("A UI/scheduler writer has no graceful stop channel; capture remains held.");
                await writer.Process.WaitForExitAsync(token).WaitAsync(CaptureGracefulBudget, token).ConfigureAwait(false);
                await writer.Completion.WaitAsync(CaptureGracefulBudget, token).ConfigureAwait(false);
            }
            return CaptureStatus();
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>Restarts missing configured children only after the owner releases its snapshot boundary.</summary>
    public async Task<CaptureLifecycleReceipt> ReleaseCaptureAsync(string lease, CancellationToken token)
    {
        await _lifecycle.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (_sync) if (captureLease != lease) throw new InvalidOperationException("Capture lifecycle lease changed.");
            await StartConfiguredProcessesAsync(token).ConfigureAwait(false);
            lock (_sync) { captureLease = null; capturedScheduler = null; capturedWriters.Clear(); captureReleased?.TrySetResult(); captureReleased = null; }
            return CaptureStatus();
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>Stops the held native source only after its original image and prefixes have been exported. Never kills.</summary>
    public async Task<CaptureLifecycleReceipt> StopCaptureNativeAsync(string lease, CancellationToken token)
    {
        await _lifecycle.WaitAsync(token).ConfigureAwait(false);
        try
        {
            OwnedProcess? source;
            lock (_sync)
            {
                if (captureLease != lease) throw new InvalidOperationException("Capture lifecycle lease changed.");
                _running.TryGetValue("market-data", out source);
                if (source is not null && !capturedWriters.Contains(source.Identity)) capturedWriters.Add(source.Identity);
            }
            if (source is not null && !HasExited(source.Process))
            {
                if (!TryRequestGracefulShutdown(source)) throw new InvalidOperationException("Native source has no graceful stop channel; capture remains held.");
                await source.Process.WaitForExitAsync(token).WaitAsync(CaptureGracefulBudget, token).ConfigureAwait(false);
                await source.Completion.WaitAsync(CaptureGracefulBudget, token).ConfigureAwait(false);
            }
            return CaptureStatus();
        }
        finally { _lifecycle.Release(); }
    }

    void ThrowIfCaptureHeld()
    {
        lock (_sync) if (captureLease is not null) throw new InvalidOperationException("Development lifecycle is held for an owned capture boundary.");
    }

    async Task EnterRecoveryLifecycleAsync(CancellationToken token)
    {
        while (true)
        {
            Task? released; lock (_sync) released = captureReleased?.Task;
            if (released is not null) await released.WaitAsync(token).ConfigureAwait(false);
            await _lifecycle.WaitAsync(token).ConfigureAwait(false);
            lock (_sync) if (captureLease is null) return;
            _lifecycle.Release();
        }
    }
}

/// <summary>Actual managed ownership evidence; a hold never attests native or database state.</summary>
public sealed record CaptureLifecycleReceipt(string State, string? LeaseId, bool ForcedStop,
    ManagedProcessIdentity? Scheduler, IReadOnlyCollection<ManagedProcessIdentity> RunningProcesses,
    IReadOnlyCollection<ManagedProcessIdentity> StoppedWriters);

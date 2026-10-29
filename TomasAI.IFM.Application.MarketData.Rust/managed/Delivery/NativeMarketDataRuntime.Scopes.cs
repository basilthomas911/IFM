using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Reads the logical options generation without starting a worker.
    /// Guid.Empty means unprepared; physical books have independent identities.</summary>
    public Guid ReadOptionOwnerGeneration()
    {
        lock (gate) { EnsureLiveAccess(); NativeMarketDataLibrary.Check(library.ScopeGeneration(handle.Identity, out var low, out var high), "read options owner generation"); return Identity(low, high); }
    }
    /// <summary>Reads physical attempt/episode and fresh readiness evidence for
    /// this scope. Owner preparation or a prior successful acquire is not enough.</summary>
    public unsafe LiveLaneStatus ReadOptionScopeStatus(string scope)
    {
        Span<byte> bytes = stackalloc byte[512]; var length = ScopeBytes(scope, bytes);
        lock (gate)
        {
            EnsureLiveAccess(); NativeLaneStatus result;
            fixed (byte* input = bytes) NativeMarketDataLibrary.Check(library.ScopeStatus(handle.Identity, input, (uint)length, 64, out result), "read options scope status");
            if (result.Size != 64 || result.Phase > 5 || result.Reserved0 != 0 || result.Reserved1 != 0) throw new InvalidDataException("Malformed scope lifecycle.");
            return new(result.Phase, result.Attempt, result.Episode, result.Attempts, Identity(result.GenerationLow, result.GenerationHigh), ReadOptionScopeFailure(scope));
        }
    }
    /// <summary>Reads lifecycle transition fields without allocating a record or
    /// decoding error text on unchanged hot quote signals. This notification
    /// optimization is not a health cache: full status queries still read every
    /// failure, and native diagnostics independently deliver all provider messages.</summary>
    public unsafe bool TryReadOptionScopeStatusChange(string scope,LiveLaneStatus? previous,out LiveLaneStatus? changed)
    {
        Span<byte> bytes=stackalloc byte[512];var length=ScopeBytes(scope,bytes);
        lock(gate)
        {
            EnsureLiveAccess();NativeLaneStatus value;
            fixed(byte* input=bytes)NativeMarketDataLibrary.Check(library.ScopeStatus(handle.Identity,input,(uint)length,64,out value),"read scope lifecycle transition");
            if(value.Size!=64||value.Phase>5||value.Reserved0!=0||value.Reserved1!=0)throw new InvalidDataException("Malformed scope lifecycle.");
            var generation=Identity(value.GenerationLow,value.GenerationHigh);
            if(previous is not null&&previous.Phase==value.Phase&&previous.Attempt==value.Attempt&&previous.Episode==value.Episode&&previous.Attempts==value.Attempts&&previous.Generation==generation)
            {changed=null;return false;}
            changed=new(value.Phase,value.Attempt,value.Episode,value.Attempts,generation,ReadOptionScopeFailure(scope));return true;
        }
    }
    /// <summary>Reads current supervisor/scope failure without consuming provider
    /// logs. A null scope selects the logical supervisor. This is independent of
    /// quote queue capacity and is checked on the native options-lane signal.</summary>
    public unsafe string? ReadOptionScopeFailure(string? scope = null)
    {
        Span<byte> bytes = stackalloc byte[512]; var length = scope is null ? 0 : ScopeBytes(scope, bytes);
        lock (gate)
        {
            EnsureLiveAccess(); NativeControlResult result;
            fixed (byte* input = bytes) NativeMarketDataLibrary.Check(library.ScopeFailure(handle.Identity, input, (uint)length, 4112, out result), "read options scope failure");
            if (result.Size != 4112 || result.Length > 4096 || result.Failure > 1 || result.Reserved != 0) throw new InvalidDataException("Malformed scope failure.");
            return result.Failure == 0 ? null : StrictUtf8.GetString(new ReadOnlySpan<byte>(result.Message, (int)result.Length));
        }
    }
    /// <summary>Signals matching native options cancellation immediately, even
    /// at an expired deadline or full control queue. This is not join proof.</summary>
    public void SignalOptionOwnerStop(Guid generation)
    {
        var bits=GuidBits(generation);
        lock(gate){EnsureLiveAccess();NativeMarketDataLibrary.Check(library.ScopeStopSignal(handle.Identity,bits.Low,bits.High),"signal options stop");}
    }
    /// <summary>Prepares/replaces the options subtree after joining its predecessor.
    /// Desired scopes must be restored afterward; this alone is not feed readiness.</summary>
    public Task PrepareOptionOwnerAsync(Guid expectedGeneration, TimeSpan remainingBudget, CancellationToken cancellationToken = default) =>
        SubmitScope(4, expectedGeneration, new { }, remainingBudget, cancellationToken);
    /// <summary>Stops options only, bypassing the control queue before awaiting
    /// proof that every physical descendant has dropped its resources.</summary>
    public Task StopOptionOwnerAsync(Guid expectedGeneration, TimeSpan remainingBudget, CancellationToken cancellationToken = default) =>
        SubmitScope(3, expectedGeneration, new { }, remainingBudget, cancellationToken);
    /// <summary>Starts or proves an identical healthy physical scope. Credentials
    /// enter only copied cold control bytes, cleared after submission. Business
    /// lease authority remains in application policy; no backend is registered.</summary>
    public Task StartOptionScopeAsync(string scope, string physicalDigest, Guid expectedGeneration, string key, string host, ushort port,
        string[] numericDnsServers, NativeSubscription[] subscriptions, NativeRegistration[] registrations, bool requireObservations,
        TimeSpan remainingBudget, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope); ArgumentException.ThrowIfNullOrWhiteSpace(physicalDigest);
        ArgumentNullException.ThrowIfNull(subscriptions); ArgumentNullException.ThrowIfNull(registrations);
        if (scope.Length > 128 || subscriptions.Length is < 1 or > 64 || registrations.Length is < 1 or > 2112) throw new ArgumentOutOfRangeException(nameof(scope));
        return SubmitScope(0, expectedGeneration, new { scope, digest = physicalDigest, request = new {
            key, host, port, dns = numericDnsServers, evidence = requireObservations ? "observations" : "control",
            subscriptions = subscriptions.Select(s => new { symbols = s.Symbols, schema = s.Schema, symbology = s.Symbology, replay_start_ns = s.ReplayStartNanoseconds }),
            registrations = registrations.Select(r => new { instrument = r.Instrument, publisher = r.Publisher, contract = r.Contract, source = r.Source, asset = r.Option ? "option" : "futures", tick = r.Tick, vwap = r.Vwap, required = r.Required })
        } }, remainingBudget, cancellationToken);
    }
    /// <summary>Resets only this scope within the existing incident budget.</summary>
    public Task ResetOptionScopeAsync(string scope, Guid expectedGeneration, TimeSpan remainingBudget, CancellationToken cancellationToken = default) =>
        SubmitScope(1, expectedGeneration, new { scope }, remainingBudget, cancellationToken);
    /// <summary>Releases and joins just this scope. An older generation's delayed
    /// release cannot remove the replacement owner's scope with the same name.</summary>
    public Task ReleaseOptionScopeAsync(string scope, Guid expectedGeneration, TimeSpan remainingBudget, CancellationToken cancellationToken = default) =>
        SubmitScope(2, expectedGeneration, new { scope }, remainingBudget, cancellationToken);
    unsafe Task SubmitScope<T>(uint action, Guid generation, T payload, TimeSpan remainingBudget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if (remainingBudget > TimeSpan.FromMinutes(2)) remainingBudget = TimeSpan.FromMinutes(2);
        var started = System.Diagnostics.Stopwatch.GetTimestamp(); var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        try
        {
            if (bytes.Length > 8 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(payload));
            var remaining = remainingBudget - System.Diagnostics.Stopwatch.GetElapsedTime(started); var identity = GuidBits(generation);
            lock (gate)
            {
                EnsureLiveAccess(); var operation = checked(++nextOperation); var request = new Pending { Control = new(TaskCreationOptions.RunContinuationsAsynchronously) }; pending.Add(operation, request);
                int status;
                fixed (byte* input = bytes) status = library.ScopeSubmit(handle.Identity, operation, action, identity.Low, identity.High, input, (uint)bytes.Length, (ulong)Math.Ceiling(Math.Max(remaining.TotalMilliseconds, 0)));
                if (status != 0) { pending.Remove(operation); NativeMarketDataLibrary.Check(status, "submit options scope"); }
                request.Cancellation = cancellationToken.UnsafeRegister(_ => library.Cancel(handle.Identity, operation), null); return request.Control.Task;
            }
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    static int ScopeBytes(string scope, Span<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope); if (scope.Length > 128) throw new ArgumentOutOfRangeException(nameof(scope)); return StrictUtf8.GetBytes(scope, bytes);
    }
    /// <summary>Reads coherent physical-scope state. Flags zero means unavailable,
    /// including while starting, retiring or replacing. This acquires no lease.</summary>
    public unsafe LivePublication ReadOptionScopeState(string scope, uint instrument, ushort publisher)
    {
        Span<byte> bytes = stackalloc byte[512]; var length = ScopeBytes(scope, bytes);
        lock (gate) { EnsureLiveAccess(); LivePublication result; fixed (byte* id = bytes) NativeMarketDataLibrary.Check(library.ScopeState(handle.Identity, id, (uint)length, instrument, publisher, 656, out result), "read options scope state");
            if (result.Size != 656 || result.Source.Size != 128 || result.Source.Kind != 0 || result.Source.Lane != MarketDataLane.OptionsGlbx || result.Flags > 63 || result.Reserved != 0) throw new InvalidDataException("Malformed scope state."); return result; }
    }
    /// <summary>Copies at most sixteen ordered source/coherent-state publications
    /// without per-record allocation. Lane bit 3 wakes the receiver; drain all
    /// desired scopes until caught up.</summary>
    public unsafe int DrainOptionScopePublications(string scope, Span<LivePublication> destination)
    {
        if (destination.Length > 16) throw new ArgumentOutOfRangeException(nameof(destination)); Span<byte> bytes = stackalloc byte[512]; var length = ScopeBytes(scope, bytes);
        lock (gate) { EnsureLiveAccess(); uint count;
            fixed (byte* id = bytes) fixed (LivePublication* output = destination) NativeMarketDataLibrary.Check(library.ScopePublications(handle.Identity, id, (uint)length, output, (uint)destination.Length, out count), "drain options scope publications");
            if (count > destination.Length) throw new InvalidDataException("Native scoped publication capacity exceeded.");
            for (var index = 0; index < count; index++) { ref var value = ref destination[index]; if (value.Size != 656 || value.Source.Size != 128 || value.Source.Lane != MarketDataLane.OptionsGlbx || value.Source.Kind is < 1 or > 6 || value.Update > 6 || value.Reserved != 0 || value.Flags > 63 || (value.Update >= 5) != (value.Source.Kind >= 5) || value.Source.Kind >= 5 && value.Update != value.Source.Kind) throw new InvalidDataException("Malformed scoped publication."); }
            return (int)count; }
    }
}

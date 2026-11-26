using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Native forwarding evidence only; it does not certify a cross-owner snapshot.</summary>
public sealed record NativeSessionForwardingStatus(bool Supported, ulong Lease, ulong ActiveSends);
/// <summary>Native source-production pause only. Relay and original book owners remain alive.</summary>
public sealed record NativeSessionProductionStatus(bool Supported, ulong Lease, ulong ActiveCallbacks);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Reads the production-pause capability and exact active callback count without changing ownership.</summary>
    public NativeSessionProductionStatus SessionProductionStatus()
        =>new(library.ProductionPause is not null&&library.ProductionResume is not null&&library.ProductionLease is not null&&library.ProductionActive is not null,
            library.ProductionLease?.Invoke()??0,library.ProductionActive?.Invoke()??0);
    /// <summary>Stops new accepted source callbacks and drains accepted callbacks without retiring original owners.</summary>
    public unsafe void PauseSourceProduction(string scope,ulong lease,TimeSpan budget)
    {
        if(lease==0||budget<=TimeSpan.Zero||budget>TimeSpan.FromSeconds(10))throw new ArgumentOutOfRangeException(nameof(budget));
        if(string.IsNullOrWhiteSpace(scope)||scope.Length>128||scope.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("A bounded original native scope is required.",nameof(scope));
        var pause=library.ProductionPause??throw new NotSupportedException("Native source-production pause is unavailable.");
        var name=System.Text.Encoding.UTF8.GetBytes(scope);
        fixed(byte* pointer=name)NativeMarketDataLibrary.Check(pause(pointer,checked((uint)name.Length),lease,checked((uint)Math.Ceiling(budget.TotalMilliseconds))),"source production pause");
        var status=SessionProductionStatus();
        if(!status.Supported||status.Lease!=lease||status.ActiveCallbacks!=0)throw new InvalidOperationException("Native source-production pause did not establish its matching drained lease.");
    }
    /// <summary>Releases only a matching production pause, without resetting original registration or books.</summary>
    public void ResumeSourceProduction(ulong lease)
    {
        if(lease==0)throw new ArgumentOutOfRangeException(nameof(lease));
        NativeMarketDataLibrary.Check((library.ProductionResume??throw new NotSupportedException("Native source-production pause is unavailable."))(lease),"source production resume");
    }
    /// <summary>Reads only forwarding ownership; this is not a complete snapshot readiness check.</summary>
    public NativeSessionForwardingStatus SessionForwardingStatus()
        => new(library.SessionHold is not null && library.SessionRelease is not null && library.SessionLease is not null && library.SessionActive is not null,
            library.SessionLease?.Invoke() ?? 0, library.SessionActive?.Invoke() ?? 0);
    /// <summary>Pauses only retained-source forwarding; provider capture remains active. Core-only runtimes refuse.</summary>
    public void HoldSourceForwarding(ulong lease, TimeSpan budget)
    {
        if (lease == 0 || budget <= TimeSpan.Zero || budget > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(budget));
        var hold = library.SessionHold ?? throw new NotSupportedException("Native session forwarding barrier is unavailable.");
        NativeMarketDataLibrary.Check(hold(lease, checked((ulong)budget.TotalMilliseconds)), "session forwarding hold");
    }
    /// <summary>Resumes only the exact matching forwarding lease; it does not change provider generations.</summary>
    public void ReleaseSourceForwarding(ulong lease)
        => NativeMarketDataLibrary.Check((library.SessionRelease ?? throw new NotSupportedException("Native session forwarding barrier is unavailable."))(lease), "session forwarding release");

    /// <summary>Copies fixed source-journal prefixes, not a frozen native mapper/required-intent restart checkpoint.</summary>
    public unsafe string CaptureSourcePrefixManifest(string scope, ulong lease)
    {
        var read = library.SessionPrefix ?? throw new NotSupportedException("Native source prefix capture is unavailable.");
        var name = System.Text.Encoding.UTF8.GetBytes(scope); var output = new byte[1024 * 1024];
        fixed (byte* namePointer = name)
        fixed (byte* outputPointer = output)
        {
            NativeMarketDataLibrary.Check(read(namePointer, checked((uint)name.Length), lease, outputPointer, checked((uint)output.Length), out var length), "source prefix snapshot");
            return System.Text.Encoding.UTF8.GetString(output.AsSpan(0, checked((int)length)));
        }
    }
}

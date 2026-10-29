using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Detached native intent identity. Keeping this record cannot keep a
/// dataset, socket or task alive, renew it, or prevent native reset.</summary>
public sealed record NativeOptionScopeClaim(Guid Generation, ulong Serial, Guid Owner);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>True only when all additive native claim commands are present.</summary>
    public bool SupportsNativeScopeClaims => library.ScopeClaim is not null
        && library.ScopeClaimRenew is not null && library.ScopeClaimRelease is not null;

    /// <summary>Copies complete business intent into native ownership. Null TTL
    /// means explicit durable intent; ephemeral intent expires inside Rust. This
    /// starts no managed renewal, expiry or provider-control task.</summary>
    public unsafe NativeOptionScopeClaim RetainNativeOptionScopes(Guid generation, Guid owner,
        string[] scopes, TimeSpan? ephemeralTimeToLive = null)
    {
        var acquire = library.ScopeClaim ?? throw new NotSupportedException("Native scope claims are unavailable in this package.");
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Length is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(scopes));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(scopes);
        if (bytes.Length > 32768) throw new ArgumentOutOfRangeException(nameof(scopes));
        var epoch = GuidBits(generation); var identity = GuidBits(owner);
        var ttl = ephemeralTimeToLive is { } duration ? ClaimTtl(duration) : 0;
        lock (gate)
        {
            EnsureLiveAccess(); NativeScopeClaimRecord result;
            fixed (byte* input = bytes) NativeMarketDataLibrary.Check(acquire(handle.Identity, epoch.Low, epoch.High,
                identity.Low, identity.High, input, (uint)bytes.Length, ttl, 64, out result), "retain native scope intent");
            if (result.Size != 64 || result.Flags != 0 || result.Reserved0 != 0 || result.Reserved1 != 0
                || result.Serial == 0 || Identity(result.GenerationLow, result.GenerationHigh) != generation
                || Identity(result.OwnerLow, result.OwnerHigh) != owner)
                throw new InvalidDataException("Malformed native scope claim identity.");
            return new(generation, result.Serial, owner);
        }
    }

    /// <summary>Explicit ephemeral command; no managed lifecycle timer is created.
    /// Expired or retired tokens cannot revive their former subscriptions.</summary>
    public void RenewNativeOptionScopes(NativeOptionScopeClaim claim, TimeSpan timeToLive)
    {
        var renew = library.ScopeClaimRenew ?? throw new NotSupportedException("Native scope claims are unavailable in this package.");
        var value = ClaimRecord(claim); var ttl = ClaimTtl(timeToLive);
        lock (gate) { EnsureLiveAccess(); NativeMarketDataLibrary.Check(renew(handle.Identity, in value, ttl), "renew native scope intent"); }
    }

    /// <summary>Removes desired intent and signals native cleanup. A copied stale
    /// generation is harmless. This command does not claim physical join proof.</summary>
    public void ReleaseNativeOptionScopes(NativeOptionScopeClaim claim)
    {
        var release = library.ScopeClaimRelease ?? throw new NotSupportedException("Native scope claims are unavailable in this package.");
        var value = ClaimRecord(claim);
        lock (gate) { EnsureLiveAccess(); NativeMarketDataLibrary.Check(release(handle.Identity, in value), "release native scope intent"); }
    }

    static ulong ClaimTtl(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(2)) throw new ArgumentOutOfRangeException(nameof(duration));
        return checked((ulong)Math.Ceiling(duration.TotalMilliseconds));
    }
    static NativeScopeClaimRecord ClaimRecord(NativeOptionScopeClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var epoch = GuidBits(claim.Generation); var owner = GuidBits(claim.Owner);
        return new() { Size = 64, GenerationLow = epoch.Low, GenerationHigh = epoch.High,
            Serial = claim.Serial, OwnerLow = owner.Low, OwnerHigh = owner.High };
    }
}

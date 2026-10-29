using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Rust.Publication;

/// <summary>Copied native provider diagnostics. Unknown codes and complete bounded
/// text are preserved; IsLast is the provider flag, not a C# fatality decision.</summary>
[MessagePackObject]
public sealed record NativeProviderDiagnostic(
    [property: Key(0)] string Dataset,
    [property: Key(1)] string Role,
    [property: Key(2)] Guid Worker,
    [property: Key(3)] ulong Attempt,
    [property: Key(4)] DateTimeOffset ObservedAtUtc,
    [property: Key(5)] uint Kind,
    [property: Key(6)] uint Code,
    [property: Key(7)] bool IsLast,
    [property: Key(8)] string Message,
    [property: Key(9)] ulong Replaced);

/// <summary>External log acceptance of copied diagnostic bytes. This receipt
/// neither proves UI display nor authorizes trade-evidence persistence.</summary>
[MessagePackObject]
public sealed record NativeDiagnosticReceipt(
    [property: Key(0)] ulong Receipt,
    [property: Key(1)] string Identity,
    [property: Key(2)] string Digest,
    [property: Key(3)] bool Accepted,
    [property: Key(4)] string? Error);

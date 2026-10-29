using System.Buffers;
using System.Text;
using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>One provider definition source line; JSON identity is preserved exactly.</summary>
public readonly record struct CatalogSourceLine(string Dataset, string Json);

/// <summary>Provider universe for an atomic cross-dataset definition refresh.</summary>
public sealed record CatalogUniverse(string Dataset, string[] Parents);

public sealed partial class NativeMarketDataRuntime
{
    static readonly UTF8Encoding CatalogUtf8 = new(false, true);
    bool catalogConfigured;
    ulong catalogRevision;

    /// <summary>Configures the native historical HTTP owner once. Numeric DNS
    /// endpoints avoid an uncancellable operating-system resolver fallback.</summary>
    public unsafe void ConfigureHistory(string endpoint, string key, string[] numericDnsServers)
    {
        var data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { endpoint, key, dns = numericDnsServers });
        try
        {
            if (data.Length > 8192) throw new ArgumentOutOfRangeException(nameof(endpoint));
            lock (gate)
            {
                EnsureLiveAccess();
                fixed (byte* input = data)
                    NativeMarketDataLibrary.Check(library.HistoryConfigure(handle.Identity, input, (uint)data.Length), "configure history");
            }
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(data); }
    }

    /// <summary>Acquires and atomically publishes provider definitions entirely
    /// inside native owned I/O. All datasets use the same remaining budget,
    /// capped at two minutes. No managed/provider subprocess is involved.</summary>
    public async Task<ulong> RefreshCatalogAsync(CatalogUniverse[] universes, TimeSpan remainingBudget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(universes);
        if (universes.Length is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(universes));
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        if (remainingBudget > TimeSpan.FromMinutes(2)) remainingBudget = TimeSpan.FromMinutes(2);
        var data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(universes.Select(u => new { dataset = u.Dataset, parents = u.Parents }));
        if (data.Length > 8192) throw new ArgumentOutOfRangeException(nameof(universes));
        var remaining = remainingBudget - System.Diagnostics.Stopwatch.GetElapsedTime(started);
        await SubmitCatalogRefresh(data, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            EnsureLiveAccess();
            NativeMarketDataLibrary.Check(library.CatalogRevision(handle.Identity, out catalogRevision), "catalog revision");
            return catalogRevision;
        }
    }

    unsafe Task SubmitCatalogRefresh(byte[] data, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            EnsureLiveAccess();
            var operation = checked(++nextOperation);
            var request = new Pending { Control = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            pending.Add(operation, request);
            int status;
            fixed (byte* input = data)
                status = library.CatalogRefresh(handle.Identity, operation, input, (uint)data.Length, (ulong)Math.Ceiling(budget.TotalMilliseconds));
            if (status != 0) { pending.Remove(operation); NativeMarketDataLibrary.Check(status, "catalog refresh"); }
            request.Cancellation = cancellationToken.UnsafeRegister(_ => library.Cancel(handle.Identity, operation), null);
            return request.Control.Task;
        }
    }

    /// <summary>Establishes cold-catalog limits once, before hydration. Native
    /// active and staging universes each obey these limits.</summary>
    public void ConfigureCatalog(uint maximumRows, uint maximumSourceBytes)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            NativeMarketDataLibrary.Check(library.CatalogConfigure(handle.Identity, maximumRows, maximumSourceBytes), "catalog configure");
            catalogConfigured = true;
        }
    }

    /// <summary>Streams a complete replacement into native ownership. Failed,
    /// cancelled or partially enumerated refreshes preserve the committed catalog.
    /// Concurrent refreshes are rejected explicitly; reads continue during loading.
    /// The caller's cancellation token must carry its original operation deadline.</summary>
    public async Task<ulong> HydrateCatalogAsync(IAsyncEnumerable<CatalogSourceLine> lines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        cancellationToken.ThrowIfCancellationRequested();
        ulong token;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            if (!catalogConfigured) throw new InvalidOperationException("Configure the native catalog before hydration.");
            NativeMarketDataLibrary.Check(library.CatalogBegin(handle.Identity, out token), "catalog begin");
        }
        var committed = false;
        try
        {
            await foreach (var line in lines.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendCatalog(token, line);
            }
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposal is not null, this);
                NativeMarketDataLibrary.Check(library.CatalogFinish(handle.Identity, token, 1, out var revision), "catalog commit");
                catalogRevision = revision;
                committed = true;
                return revision;
            }
        }
        finally
        {
            if (!committed)
            {
                lock (gate)
                {
                    // Disposal owns all remaining native state. An invalid source
                    // may already have aborted this token; do not mask its error.
                    if (disposal is null) _ = library.CatalogFinish(handle.Identity, token, 0, out _);
                }
            }
        }
    }

    unsafe void AppendCatalog(ulong token, CatalogSourceLine line)
    {
        ArgumentNullException.ThrowIfNull(line.Dataset);
        ArgumentNullException.ThrowIfNull(line.Json);
        int datasetLength = CatalogUtf8.GetByteCount(line.Dataset), sourceLength = CatalogUtf8.GetByteCount(line.Json);
        if (datasetLength is < 1 or > 64 || sourceLength is < 1 or > 65_536) throw new ArgumentOutOfRangeException(nameof(line));
        Span<byte> dataset = stackalloc byte[64];
        var source = ArrayPool<byte>.Shared.Rent(65_536);
        try
        {
            CatalogUtf8.GetBytes(line.Dataset, dataset);
            CatalogUtf8.GetBytes(line.Json, source);
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposal is not null, this);
                fixed (byte* datasetPointer = dataset)
                fixed (byte* sourcePointer = source)
                    NativeMarketDataLibrary.Check(library.CatalogAppend(handle.Identity, token, datasetPointer, (uint)datasetLength, sourcePointer, (uint)sourceLength), "catalog append");
            }
        }
        finally { ArrayPool<byte>.Shared.Return(source); }
    }

    /// <summary>Reads one uniquely resolved source definition. Null means absent;
    /// duplicate canonical identities and stale/native failures are explicit errors.
    /// No subscription or provider request is acquired by reading the catalog.</summary>
    public unsafe string? ReadCatalogDefinition(string canonicalContractId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalContractId);
        int idLength = CatalogUtf8.GetByteCount(canonicalContractId);
        if (idLength > 256) throw new ArgumentOutOfRangeException(nameof(canonicalContractId));
        Span<byte> id = stackalloc byte[256];
        CatalogUtf8.GetBytes(canonicalContractId, id);
        var output = ArrayPool<byte>.Shared.Rent(65_536);
        try
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposal is not null, this);
                fixed (byte* idPointer = id)
                fixed (byte* outputPointer = output)
                {
                    NativeMarketDataLibrary.Check(library.CatalogResolve(handle.Identity, catalogRevision, idPointer, (uint)idLength, outputPointer, 65_536, out var length), "catalog resolve");
                    if (length > 65_536) throw new InvalidDataException("Native catalog exceeded its caller buffer.");
                    return length == 0 ? null : CatalogUtf8.GetString(output.AsSpan(0, (int)length));
                }
            }
        }
        finally { ArrayPool<byte>.Shared.Return(output); }
    }
}

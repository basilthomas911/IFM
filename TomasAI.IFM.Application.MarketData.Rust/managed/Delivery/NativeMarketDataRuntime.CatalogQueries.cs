using System.Buffers;
using System.Runtime.InteropServices;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Interop;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Native-parsed cold contract projection. Prices and timestamps retain
/// their exact provider integers. Public option/current-future DTOs are projected in Rust.</summary>
public sealed record NativeContractDefinition(string Dataset, ushort Publisher, uint Instrument,
    string Symbol, string Asset, byte Kind, string Currency, string Exchange, string SettlementCurrency,
    string Underlying, uint UnderlyingInstrument, ulong ReceivedNanoseconds, ulong EventNanoseconds,
    bool Deleted, ulong? Activation, ulong? Expiration, DateOnly? Maturity, long? Strike,
    long? Tick, long? TickAmount, int? Multiplier, byte? MaturityWeek, string? CanonicalId,
    DateOnly? QueryMaturity, bool QueryMaturityInvalid, string SecurityType, string Cfi, string UnitOfMeasure,string Class="");

/// <summary>Owns a bounded immutable catalog query. Dispose releases pinned source
/// rows; runtime disposal also releases them. Refreshes cannot change its results.</summary>
public sealed class NativeCatalogSnapshot : IDisposable
{
    readonly NativeMarketDataRuntime runtime;
    readonly ulong token;
    int disposed;
    /// <summary>Exact immutable number of result rows.</summary>
    public int Count { get; }
    internal NativeCatalogSnapshot(NativeMarketDataRuntime runtime, ulong token, int count) { this.runtime = runtime; this.token = token; Count = count; }
    /// <summary>Reads one already-parsed native definition projection.</summary>
    public NativeContractDefinition ReadDefinition(int index)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index); ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
        return runtime.ReadCatalogQueryProjection(token, (uint)index);
    }
    /// <summary>Requests native current/term-structure selection. This object
    /// pins copied definitions only, with no live provider resource.</summary>
    public TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[] SelectCurrentFutures(
        string symbol, DateOnly date, int count, string? currencyFallback)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        return runtime.SelectCurrentFutures(token, symbol, date, count, currencyFallback);
    }
    /// <summary>Copies already mapped/ordered native option DTOs. The mappings
    /// are immutable reference data and never transfer a live source handle.</summary>
    public TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel[] ReadOptionModels(string? fallback,
        NativeOptionModelUnderlying[] underlyings,bool orderByMaturity=false,DateOnly? from=null,DateOnly? through=null,string? configuredId=null,string? sourceSymbol=null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        return runtime.ReadOptionModels(token,fallback,underlyings,orderByMaturity,from,through,configuredId,sourceSymbol);
    }
    /// <summary>Copies a configured future projected by Rust without local currency or maturity calculations.</summary>
    public TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel ReadConfiguredFuture(string id,string source,string? fallback,bool onTheRun,bool rollover)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        return runtime.ReadConfiguredFuture(token,id,source,fallback,onTheRun,rollover);
    }
    /// <summary>Reads unmodified provider JSON for persistence and identity hashing.</summary>
    public string ReadSource(int index)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index); ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
        return runtime.ReadCatalogQuerySource(token, (uint)index);
    }
    /// <summary>Applies qualified native chain filtering to these immutable rows.
    /// The returned snapshot has independent ownership and must also be disposed.</summary>
    public NativeCatalogSnapshot SelectChain(DateOnly maturity, string underlying, string selectedUnderlyingId,
        byte rights = 3, byte policy = 2)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        return runtime.SelectCatalogChain(token, maturity, underlying, selectedUnderlyingId, rights, policy);
    }
    /// <summary>Releases only this query, idempotently. The backend is responsible
    /// for disposing every acquired snapshot when mapping/persistence fails.</summary>
    public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) runtime.CloseCatalogQuery(token); }
}

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Acquires an exact provider parent scope through native owned HTTP,
    /// then returns an immutable query. It does not replace the live catalog.
    /// Disposing the snapshot releases retained source ownership.</summary>
    public Task<NativeCatalogSnapshot> AcquireDefinitionSnapshotAsync(string dataset, string[] parents,
        TimeSpan remainingBudget, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset); ArgumentNullException.ThrowIfNull(parents);
        if (dataset.Length > 64 || parents.Length > 16 || parents.Any(p => p is null || p.Length > 64)) throw new ArgumentOutOfRangeException(nameof(parents));
        return AcquireDefinitions(new { dataset, parents }, remainingBudget, cancellationToken);
    }
    /// <summary>Acquires configured exact raw provider symbols, sharing native
    /// bounded decoding, cancellation and snapshot ownership with discovery.
    /// Missing symbols remain missing; no nearest-contract fallback is used.</summary>
    public Task<NativeCatalogSnapshot> AcquireExactDefinitionSnapshotAsync(string dataset, string[] symbols,
        TimeSpan remainingBudget, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset); ArgumentNullException.ThrowIfNull(symbols);
        if (dataset.Length > 64 || symbols.Length is < 1 or > 2048 || symbols.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 70))
            throw new ArgumentOutOfRangeException(nameof(symbols));
        return AcquireDefinitions(new { dataset, symbols }, remainingBudget, cancellationToken);
    }
    unsafe Task<NativeCatalogSnapshot> AcquireDefinitions<T>(T payload, TimeSpan remainingBudget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if (remainingBudget > TimeSpan.FromMinutes(2)) remainingBudget = TimeSpan.FromMinutes(2);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var data = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (data.Length > 2 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(payload));
        var remaining = remainingBudget - System.Diagnostics.Stopwatch.GetElapsedTime(started);
        lock (gate)
        {
            EnsureLiveAccess();
            var operation = checked(++nextOperation);
            var request = new Pending { CatalogQuery = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            pending.Add(operation, request);
            int status;
            fixed (byte* input = data)
                status = library.DefinitionsQuery(handle.Identity, operation, input, (uint)data.Length, (ulong)Math.Ceiling(Math.Max(remaining.TotalMilliseconds, 0)));
            if (status != 0) { pending.Remove(operation); NativeMarketDataLibrary.Check(status, "acquire definitions"); }
            request.Cancellation = cancellationToken.UnsafeRegister(_ => library.Cancel(handle.Identity, operation), null);
            return request.CatalogQuery.Task;
        }
    }

    unsafe void CompleteCatalogQuery(Pending request, NativeCompletion record)
    {
        var completion = request.CatalogQuery!;
        ulong token = 0;
        try
        {
            NativeMarketDataLibrary.Check(library.TakeLive(handle.Identity, record.Operation, 4112, out var result), "take definition query result");
            if (result.Size != 4112 || result.Length > 4096 || result.Failure > 2 || result.Reserved != 0) throw new InvalidDataException("Malformed definition query completion.");
            int count = 0;
            if (result.Failure == 0)
            {
                using var payload = JsonDocument.Parse(new ReadOnlySpan<byte>(result.Message, (int)result.Length).ToArray());
                token = payload.RootElement.GetProperty("token").GetUInt64(); count = payload.RootElement.GetProperty("count").GetInt32();
                if (token == 0 || count is < 0 or > 100_000) throw new InvalidDataException("Malformed native definition query identity.");
            }
            if (record.Outcome == 0 && result.Failure == 0)
            {
                completion.TrySetResult(new(this, token, count));
                token = 0; // Ownership transferred to the caller's snapshot.
            }
            else if (record.Outcome == 1) completion.TrySetCanceled();
            else if (record.Outcome == 2) completion.TrySetException(new TimeoutException("Native definition query original deadline expired."));
            else if (record.Outcome == 0 && result.Failure == 1) completion.TrySetException(new TomasAI.IFM.Framework.MarketData.DataBento.DatabentoFeedException(
                TomasAI.IFM.Framework.MarketData.DataBento.DatabentoFeedStatus.DatabentoError, StrictUtf8.GetString(new ReadOnlySpan<byte>(result.Message, (int)result.Length))));
            else completion.TrySetException(new InvalidOperationException(StrictUtf8.GetString(new ReadOnlySpan<byte>(result.Message, (int)result.Length))));
        }
        catch (Exception error) { completion.TrySetException(error); throw; }
        finally
        {
            // The native owner may notice expiry immediately after the query was
            // created. Even a failed completion must release that retained query.
            if (token != 0) NativeMarketDataLibrary.Check(library.CatalogQueryClose(handle.Identity, token), "close interrupted definition query");
        }
    }
    /// <summary>Pins all current source definitions, optionally within one dataset.</summary>
    public NativeCatalogSnapshot OpenCatalogSnapshot(string? dataset = null) => OpenCatalogQuery(new { kind = "all", dataset = dataset ?? "" });
    /// <summary>Pins exact native chain selection using the qualified C# filter,
    /// ordering and duplicate semantics. Policy: parent=1, future=2, roots=3.</summary>
    public NativeCatalogSnapshot OpenCatalogChain(DateOnly maturity, string underlying, byte policy = 2,
        byte rights = 3, string? selectedUnderlyingId = null, string? dataset = null) =>
        OpenCatalogQuery(new { kind = "chain", maturity = maturity.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            underlying, policy, rights, selected = selectedUnderlyingId, dataset = dataset ?? "" });
    internal NativeCatalogSnapshot SelectCatalogChain(ulong token, DateOnly maturity, string underlying, string selected, byte rights, byte policy) =>
        OpenCatalogQuery(new { kind = "chain", source_token = token, maturity = maturity.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            underlying, policy, rights, selected });

    unsafe NativeCatalogSnapshot OpenCatalogQuery<T>(T request)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(request);
        if (data.Length > 8192) throw new ArgumentOutOfRangeException(nameof(request));
        lock (gate)
        {
            EnsureLiveAccess();
            NativeMarketDataLibrary.Check(library.CatalogRevision(handle.Identity, out var revision), "catalog revision");
            fixed (byte* input = data)
            {
                NativeMarketDataLibrary.Check(library.CatalogQuery(handle.Identity, revision, input, (uint)data.Length, out var result), "catalog query");
                if (result.Size != 24 || result.Reserved != 0 || result.ReservedTail != 0 || result.Token == 0 || result.Count > 100_000)
                    throw new InvalidDataException("Malformed native catalog query identity.");
                return new(this, result.Token, checked((int)result.Count));
            }
        }
    }
    internal unsafe NativeContractDefinition ReadCatalogQueryProjection(ulong token, uint index)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(262_144);
        try
        {
            lock (gate)
            {
                EnsureLiveAccess();
                fixed (byte* output = buffer)
                {
                    NativeMarketDataLibrary.Check(library.CatalogQueryRow(handle.Identity, token, index, 1, output, 262_144, out var length), "catalog query projection");
                    if (length > 262_144) throw new InvalidDataException("Native projection exceeded its caller buffer.");
                    return JsonSerializer.Deserialize<NativeContractDefinition>(buffer.AsSpan(0, (int)length)) ?? throw new InvalidDataException("Missing native definition projection.");
                }
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    internal unsafe string ReadCatalogQuerySource(ulong token, uint index)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65_536);
        try
        {
            lock (gate)
            {
                EnsureLiveAccess();
                fixed (byte* output = buffer)
                {
                    NativeMarketDataLibrary.Check(library.CatalogQueryRow(handle.Identity, token, index, 0, output, 65_536, out var length), "catalog query source");
                    if (length > 65_536) throw new InvalidDataException("Native source exceeded its caller buffer.");
                    return CatalogUtf8.GetString(buffer.AsSpan(0, (int)length));
                }
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    internal void CloseCatalogQuery(ulong token)
    {
        lock (gate)
        {
            if (disposal is null) NativeMarketDataLibrary.Check(library.CatalogQueryClose(handle.Identity, token), "catalog query close");
        }
    }
}

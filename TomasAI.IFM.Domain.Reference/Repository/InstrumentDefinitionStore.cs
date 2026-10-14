
using TomasAI.IFM.Application.Storage.ReferenceDb;
using System.Text.Json;


using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;



namespace TomasAI.IFM.Domain.Reference.Repository;

/// <summary>Exact records and their query projection share an atomically published snapshot.</summary>
public sealed class InstrumentDefinitionStore(IReferenceDbContext db, ITradeStrategySymbolStore symbols) : IInstrumentDefinitionStore
{
    public const int BucketCount = 128;
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertAsync(Guid snapshot, long index, ExactInstrumentDefinition row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (snapshot == Guid.Empty || index < 0) throw new ArgumentException("A snapshot and nonnegative record index are required.",nameof(snapshot));
        return db.InsertInstrumentDefinitionAsync(snapshot,index,row,cancellationToken);
    }
    /// <inheritdoc />
    public Task<InstrumentDefinitionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        return db.GetInstrumentDefinitionSnapshotAsync(cancellationToken);
    }

    public async Task PublishAsync(InstrumentDefinitionSnapshot snapshot, IReadOnlyCollection<TradeStrategyProduct> products, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Id == Guid.Empty || snapshot.RecordCount <= 0 || products.Count == 0) throw new ArgumentException("Cannot publish an empty instrument-definition snapshot.",nameof(snapshot));
        // Preserve the stable product IDs already referenced by saved trade strategy families.
        await Parallel.ForEachAsync(products.Distinct(), new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken }, async (product, token) =>
        {
            var symbol = await symbols.GetOrCreateAsync(product, token).ConfigureAwait(false);
            if (symbol != product.WithId(symbol.Id) || symbol.Validate().Count != 0) throw new InvalidOperationException("Stored symbol does not match the definition product.");
            await db.InsertInstrumentDefinitionProductAsync(snapshot.Id,product.Family,symbol,token).ConfigureAwait(false);
        }).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        // Readers continue using the previous complete snapshot until every definition and product is durable.
        await db.PublishInstrumentDefinitionSnapshotAsync(snapshot,cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public Task<TradeStrategySymbolReadModel[]> GetSymbolsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken)
    {
        return db.GetInstrumentDefinitionSymbolsAsync(snapshot,family,cancellationToken);
    }
    public async Task<IReadOnlyList<TradeStrategyProduct>> GetProductsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken)
        => (await GetSymbolsAsync(snapshot, family, cancellationToken).ConfigureAwait(false)).Select(x => new TradeStrategyProduct(family, x.Symbol, x.Currency, x.Exchange)).ToArray();
    /// <inheritdoc />
    public IAsyncEnumerable<string> ReadJsonAsync(Guid snapshot, string dataset, int bucket, CancellationToken cancellationToken = default)
    {
        if (bucket is < 0 or >= BucketCount) throw new ArgumentOutOfRangeException(nameof(bucket));
        return db.ReadInstrumentDefinitionJsonAsync(snapshot,dataset,bucket,cancellationToken);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task IndexSelectionAsync(InstrumentDefinitionSelection value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SnapshotId == Guid.Empty || value.InstrumentClass is not ("F" or "C" or "P")
            || value.InstrumentId == 0 || value.PublisherId == 0 || string.IsNullOrWhiteSpace(value.Root)
            || value.DefinitionDigest.Length != 64)
            throw new ArgumentException("Invalid provider definition index entry.",nameof(value));
        return db.InsertInstrumentDefinitionSelectionAsync(value,cancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<InstrumentDefinitionPage> GetSelectionPageAsync(
        InstrumentDefinitionPageRequest request,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Definition catalog unavailable. Refresh stored definitions first.");
        var indexed = await db.IsInstrumentDefinitionSelectionCompleteAsync(snapshot.Id,cancellationToken).ConfigureAwait(false);
        if (!indexed)
            throw new InvalidOperationException("The stored catalog predates the selector index. Refresh definitions before browsing.");
        if (request.SnapshotId != Guid.Empty && request.SnapshotId != snapshot.Id)
            throw new InvalidOperationException("Definition catalog changed. Restart the search.");

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(request with { SnapshotId = snapshot.Id, ContinuationToken = null }))));
        var after = "";
        if (!string.IsNullOrEmpty(request.ContinuationToken))
        {
            Cursor cursor;
            try
            {
                cursor = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(request.ContinuationToken))!;
            }
            catch (Exception e) when (e is FormatException or JsonException)
            {
                throw new ArgumentException("Invalid definition page cursor.", nameof(request), e);
            }
            if (cursor is null || !string.Equals(cursor.Fingerprint,fingerprint,StringComparison.Ordinal) || cursor.After is not { Length: 16 }
                || cursor.After.Any(c => c != ':' && !char.IsAsciiDigit(c)))
                throw new ArgumentException("Definition cursor does not match the search.", nameof(request));
            after = cursor.After;
        }

        var rows = (await db.GetInstrumentDefinitionSelectionAsync(snapshot.Id,request.Dataset,request.Root,request.Options,after,request.PageSize + 1,cancellationToken).ConfigureAwait(false)).ToArray();
        var page = rows.Take(request.PageSize).ToArray();
        if (page.Any(x => x.Value is null || x.Value.SnapshotId != snapshot.Id || !string.Equals(x.Value.Dataset,request.Dataset,StringComparison.Ordinal)
            || !string.Equals(x.Value.Root,request.Root,StringComparison.Ordinal) || (!string.Equals(x.Value.InstrumentClass,"F",StringComparison.Ordinal)) != request.Options))
            throw new InvalidDataException("Definition index identity conflict.");

        bool Matches(InstrumentDefinitionSelection x) =>
            (request.IncludeExpiredOrDeleted || !x.Deleted && x.ExpirationUtc > at && (x.ActivationUtc is null || x.ActivationUtc <= at))
            && (request.Exchange is null || string.Equals(x.Exchange,request.Exchange,StringComparison.Ordinal))
            && (request.Expiry is null || x.ExpirationUtc is { } expiry && DateOnly.FromDateTime(expiry.UtcDateTime) == request.Expiry)
            && (request.UnderlyingInstrumentId is null || x.UnderlyingInstrumentId == request.UnderlyingInstrumentId)
            && (request.Right == ReferenceOptionRight.Unknown || string.Equals(x.InstrumentClass,(request.Right == ReferenceOptionRight.Call ? "C" : "P"),StringComparison.Ordinal))
            && (request.MinimumStrike is null || x.Strike >= request.MinimumStrike)
            && (request.MaximumStrike is null || x.Strike <= request.MaximumStrike);
        var next = rows.Length > request.PageSize
            ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(fingerprint, page[^1].Key)))
            : null;
        return new(snapshot.Id, snapshot.CompletedUtc, page.Select(x => x.Value).Where(Matches).ToArray(), next);
    }

    sealed record Cursor(string Fingerprint, string After);
}

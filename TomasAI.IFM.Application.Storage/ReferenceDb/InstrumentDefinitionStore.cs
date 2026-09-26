using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Exact records and their query projection share an atomically published snapshot.</summary>
public sealed class InstrumentDefinitionStore(IObjectRepository db, ITradeStrategySymbolStore symbols) : IInstrumentDefinitionStore
{
    public const int BucketCount = 128;
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS instrument_definition (
            snapshot_id uuid, dataset text, bucket int, record_index bigint,
            publisher_id int, instrument_id bigint, raw_symbol text, asset text,
            instrument_class text, currency text, exchange text, definition_json text,
            PRIMARY KEY ((snapshot_id,dataset,bucket),record_index));
        """;
    public const string CreateProductTable = """
        CREATE TABLE IF NOT EXISTS instrument_definition_product (
            snapshot_id uuid, family int, symbol text, exchange text, currency text, symbol_id int,
            PRIMARY KEY ((snapshot_id,family),symbol,exchange,currency));
        """;
    public const string CreateSnapshotTable = """
        CREATE TABLE IF NOT EXISTS instrument_definition_snapshot (
            catalog text PRIMARY KEY, snapshot_id uuid, completed_utc timestamp, record_count bigint, datasets_json text);
        """;
    public const string CreateSelectionStatusTable = """
        CREATE TABLE IF NOT EXISTS instrument_definition_selection_status(snapshot_id uuid PRIMARY KEY, complete boolean);
        """;
    public const string CreateSelectionTable = """
        CREATE TABLE IF NOT EXISTS instrument_definition_selection (
            snapshot_id uuid, dataset text, root text, options boolean, instrument_key text, payload blob,
            PRIMARY KEY ((snapshot_id,dataset,root,options),instrument_key));
        """;
    const string Insert = """
        INSERT INTO instrument_definition (snapshot_id,dataset,bucket,record_index,publisher_id,instrument_id,raw_symbol,asset,instrument_class,currency,exchange,definition_json)
        VALUES (:snapshot,:dataset,:bucket,:record_index,:publisher,:instrument,:raw,:asset,:class,:currency,:exchange,:json);
        """;
    public async Task InsertAsync(Guid snapshot, long index, ExactInstrumentDefinition row, CancellationToken cancellationToken)
    {
        if (snapshot == Guid.Empty || index < 0) throw new ArgumentException("A snapshot and nonnegative record index are required.");
        await db.Use("InstrumentDefinition.Insert", Insert).SetParameters(new Parameters([
            snapshot, row.Dataset, (int)(index % BucketCount), index, (int)row.PublisherId, (long)row.InstrumentId,
            row.RawSymbol, row.Asset, row.InstrumentClass, row.Currency, row.Exchange, row.Json])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<InstrumentDefinitionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken)
        => (await db.Use("InstrumentDefinition.Snapshot", "SELECT snapshot_id,completed_utc,record_count,datasets_json FROM instrument_definition_snapshot WHERE catalog='current';")
            .ExecuteQueryAsync(row => new InstrumentDefinitionSnapshot(row.GetGuid(0), row.GetDateTime(1), row.GetLong(2), JsonSerializer.Deserialize<string[]>(row.GetString(3))!), cancellationToken).ConfigureAwait(false)).SingleOrDefault();

    public async Task PublishAsync(InstrumentDefinitionSnapshot snapshot, IReadOnlyCollection<TradeStrategyProduct> products, CancellationToken cancellationToken)
    {
        if (snapshot.Id == Guid.Empty || snapshot.RecordCount <= 0 || products.Count == 0) throw new ArgumentException("Cannot publish an empty instrument-definition snapshot.");
        // Preserve the stable product IDs already referenced by saved trade strategy families.
        await Parallel.ForEachAsync(products.Distinct(), new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken }, async (product, token) =>
        {
            var symbol = await symbols.GetOrCreateAsync(product, token).ConfigureAwait(false);
            if (symbol != product.WithId(symbol.Id) || symbol.Validate().Count != 0) throw new InvalidOperationException("Stored symbol does not match the definition product.");
            await db.Use("InstrumentDefinition.Product", "INSERT INTO instrument_definition_product(snapshot_id,family,symbol,exchange,currency,symbol_id) VALUES(:snapshot,:family,:symbol,:exchange,:currency,:id);")
                .SetParameters(new Parameters([snapshot.Id, (int)product.Family, product.Symbol, product.Exchange, product.Currency, symbol.Id])).ExecuteCommandAsync(token).ConfigureAwait(false);
        }).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        // Readers continue using the previous complete snapshot until every definition and product is durable.
        await db.Use("InstrumentDefinition.SelectionComplete",
                "INSERT INTO instrument_definition_selection_status(snapshot_id,complete) VALUES(:snapshot,true);")
            .SetParameters(new Parameters([snapshot.Id])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await db.Use("InstrumentDefinition.Publish", "INSERT INTO instrument_definition_snapshot(catalog,snapshot_id,completed_utc,record_count,datasets_json) VALUES('current',:snapshot,:completed,:count,:datasets);")
            .SetParameters(new Parameters([snapshot.Id, snapshot.CompletedUtc, snapshot.RecordCount, JsonSerializer.Serialize(snapshot.Datasets)])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<TradeStrategySymbolReadModel[]> GetSymbolsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken)
    {
        var rows = await db.Use("InstrumentDefinition.Symbols", "SELECT symbol_id,symbol,currency,exchange FROM instrument_definition_product WHERE snapshot_id=:snapshot AND family=:family;")
            .SetParameters(new Parameters([snapshot, (int)family]))
            .ExecuteQueryAsync(row => new TradeStrategyProduct(family, row.GetString(1), row.GetString(2), row.GetString(3)).WithId(row.GetInt(0)), cancellationToken).ConfigureAwait(false);
        return rows.ToArray();
    }
    public async Task<IReadOnlyList<TradeStrategyProduct>> GetProductsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken)
        => (await GetSymbolsAsync(snapshot, family, cancellationToken).ConfigureAwait(false)).Select(x => new TradeStrategyProduct(family, x.Symbol, x.Currency, x.Exchange)).ToArray();

    public IAsyncEnumerable<string> ReadJsonAsync(Guid snapshot, string dataset, int bucket, CancellationToken cancellationToken = default)
    {
        if (bucket is < 0 or >= BucketCount) throw new ArgumentOutOfRangeException(nameof(bucket));
        return db.Use("InstrumentDefinition.ReadJson", "SELECT definition_json FROM instrument_definition WHERE snapshot_id=:snapshot AND dataset=:dataset AND bucket=:bucket;")
            .SetParameters(new Parameters([snapshot, dataset, bucket])).ExecuteStreamAsync(row => row.GetString(0), cancellationToken);
    }

    public async Task IndexSelectionAsync(InstrumentDefinitionSelection value, CancellationToken cancellationToken)
    {
        if (value.SnapshotId == Guid.Empty || value.InstrumentClass is not ("F" or "C" or "P")
            || value.InstrumentId == 0 || value.PublisherId == 0 || string.IsNullOrWhiteSpace(value.Root)
            || value.DefinitionDigest.Length != 64)
            throw new ArgumentException("Invalid provider definition index entry.");
        var key = FormattableString.Invariant($"{value.PublisherId:D5}:{value.InstrumentId:D10}");
        await db.Use("InstrumentDefinition.IndexSelection", """
                INSERT INTO instrument_definition_selection(snapshot_id,dataset,root,options,instrument_key,payload)
                VALUES(:snapshot,:dataset,:root,:options,:key,:payload);
                """)
            .SetParameters(new Parameters([
                value.SnapshotId,
                value.Dataset,
                value.Root,
                value.InstrumentClass != "F",
                key,
                MessagePackBinarySerializer.Shared.Serialize(value)!
            ]))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<InstrumentDefinitionPage> GetSelectionPageAsync(
        InstrumentDefinitionPageRequest request,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        request.Validate();
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Definition catalog unavailable. Refresh stored definitions first.");
        var indexed = await db.Use(
                "InstrumentDefinition.SelectionStatus",
                "SELECT complete FROM instrument_definition_selection_status WHERE snapshot_id=:snapshot;")
            .SetParameters(new Parameters([snapshot.Id]))
            .ExecuteQueryAsync(row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false);
        if (!indexed.SingleOrDefault())
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
                throw new ArgumentException("Invalid definition page cursor.", e);
            }
            if (cursor is null || cursor.Fingerprint != fingerprint || cursor.After is not { Length: 16 }
                || cursor.After.Any(c => c != ':' && !char.IsAsciiDigit(c)))
                throw new ArgumentException("Definition cursor does not match the search.");
            after = cursor.After;
        }

        var rows = (await db.Use("InstrumentDefinition.SelectionPage", """
                SELECT instrument_key,payload FROM instrument_definition_selection
                WHERE snapshot_id=:snapshot AND dataset=:dataset AND root=:root AND options=:options
                AND instrument_key>:after LIMIT :page_limit;
                """)
            .SetParameters(new Parameters([
                snapshot.Id,
                request.Dataset,
                request.Root,
                request.Options,
                after,
                request.PageSize + 1
            ]))
            .ExecuteQueryAsync(
                row => (
                    Key: row.GetString(0),
                    Value: MessagePackBinarySerializer.Shared.Deserialize<InstrumentDefinitionSelection>(row.GetBytes(1))!),
                cancellationToken)
            .ConfigureAwait(false)).ToArray();
        var page = rows.Take(request.PageSize).ToArray();
        if (page.Any(x => x.Value is null || x.Value.SnapshotId != snapshot.Id || x.Value.Dataset != request.Dataset
            || x.Value.Root != request.Root || (x.Value.InstrumentClass != "F") != request.Options))
            throw new InvalidDataException("Definition index identity conflict.");

        bool Matches(InstrumentDefinitionSelection x) =>
            (request.IncludeExpiredOrDeleted || !x.Deleted && x.ExpirationUtc > at && (x.ActivationUtc is null || x.ActivationUtc <= at))
            && (request.Exchange is null || x.Exchange == request.Exchange)
            && (request.Expiry is null || x.ExpirationUtc is { } expiry && DateOnly.FromDateTime(expiry.UtcDateTime) == request.Expiry)
            && (request.UnderlyingInstrumentId is null || x.UnderlyingInstrumentId == request.UnderlyingInstrumentId)
            && (request.Right == ReferenceOptionRight.Unknown || x.InstrumentClass == (request.Right == ReferenceOptionRight.Call ? "C" : "P"))
            && (request.MinimumStrike is null || x.Strike >= request.MinimumStrike)
            && (request.MaximumStrike is null || x.Strike <= request.MaximumStrike);
        var next = rows.Length > request.PageSize
            ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(fingerprint, page[^1].Key)))
            : null;
        return new(snapshot.Id, snapshot.CompletedUtc, page.Select(x => x.Value).Where(Matches).ToArray(), next);
    }

    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
    sealed record Cursor(string Fingerprint, string After);
}

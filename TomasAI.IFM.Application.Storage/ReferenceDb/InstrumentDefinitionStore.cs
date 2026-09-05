using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.Storage;

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
    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
}

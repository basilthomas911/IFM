using TomasAI.IFM.Application.MarketData.Contracts;
using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public sealed class TradeStrategySymbolStore(IDbContextFactory db, ISequenceIdGenerator sequenceIds) : ITradeStrategySymbolStore
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS trade_strategy_symbol (
          family int, exchange text, symbol text, currency text, id int,
          PRIMARY KEY ((family), exchange, symbol, currency));
        """;
    const string Select = "SELECT id,symbol,currency,exchange FROM reference.trade_strategy_symbol WHERE family=$1";
    public async Task<TradeStrategySymbolReadModel> GetOrCreateAsync(TradeStrategyProduct product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);
        product.Validate();
        var existing = await FindProduct(product, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return existing;
        var id = checked((int)await sequenceIds.GetSequenceIdAsync(SequenceName.Reference_TradeStrategySymbolId, cancellationToken).ConfigureAwait(false));
        if (id <= 0) throw new InvalidOperationException("The product sequence returned a non-positive identity.");
        await db.ReferenceDb.Use("TradeStrategySymbol.Insert", """
            INSERT INTO reference.trade_strategy_symbol (family,exchange,symbol,currency,id)
            VALUES ($1,$2,$3,$4,$5) ON CONFLICT(family,exchange,symbol,currency) DO NOTHING;
            """).SetParameters(new Parameters([Integer((int)product.Family), Text(product.Exchange), Text(product.Symbol), Text(product.Currency), Integer(id)]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return await FindProduct(product, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The product identity insert could not be read back.");
    }
    async Task<TradeStrategySymbolReadModel?> FindProduct(TradeStrategyProduct product, CancellationToken cancellationToken) =>
        (await db.ReferenceDb.Use("TradeStrategySymbol.ByKey", Select + " AND exchange=$2 AND symbol=$3 AND currency=$4;")
            .SetParameters(new Parameters([Integer((int)product.Family), Text(product.Exchange), Text(product.Symbol), Text(product.Currency)]))
            .ExecuteQueryAsync(x => Map(product.Family, x), cancellationToken).ConfigureAwait(false)).SingleOrDefault();
    public async Task<TradeStrategySymbolReadModel?> FindAsync(TradeStrategyFamilyType family, int id, CancellationToken cancellationToken) =>
        (await db.ReferenceDb.Use("TradeStrategySymbol.ByFamily", Select + ";")
            .SetParameters(new Parameters([Integer((int)family)]))
            .ExecuteQueryAsync(x => Map(family, x), cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.Id == id);
    static TradeStrategySymbolReadModel Map(TradeStrategyFamilyType family, IObjectDataRecord row) =>
        new TradeStrategyProduct(family, row.GetString(1), row.GetString(2), row.GetString(3)).WithId(row.GetInt(0));
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
readonly record struct Parameters(NpgsqlParameter[] Values) : IBindValue { public object Bind() => Values; }
}

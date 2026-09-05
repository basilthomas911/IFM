using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public sealed class TradeStrategySymbolStore(IDbContextFactory db, ISequenceIdGenerator sequenceIds) : ITradeStrategySymbolStore
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS trade_strategy_symbol_v1 (
          family int, exchange text, symbol text, currency text, id int,
          PRIMARY KEY ((family), exchange, symbol, currency));
        """;
    const string Select = "SELECT id,symbol,currency,exchange FROM trade_strategy_symbol_v1 WHERE family=:family";
    public async Task<TradeStrategySymbolReadModel> GetOrCreateAsync(TradeStrategyProduct product, CancellationToken cancellationToken)
    {
        product.Validate();
        var existing = await FindProduct(product, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return existing;
        var id = checked((int)await sequenceIds.GetSequenceIdAsync(SequenceName.Reference_TradeStrategySymbolId, cancellationToken).ConfigureAwait(false));
        if (id <= 0) throw new InvalidOperationException("The product sequence returned a non-positive identity.");
        await db.ReferenceDb.Use("TradeStrategySymbol.Insert", """
            INSERT INTO trade_strategy_symbol_v1 (family,exchange,symbol,currency,id)
            VALUES (:family,:exchange,:symbol,:currency,:id) IF NOT EXISTS;
            """).SetParameters(new Parameters([(int)product.Family, product.Exchange, product.Symbol, product.Currency, id]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return await FindProduct(product, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The product identity insert could not be read back.");
    }
    async Task<TradeStrategySymbolReadModel?> FindProduct(TradeStrategyProduct product, CancellationToken cancellationToken) =>
        (await db.ReferenceDb.Use("TradeStrategySymbol.ByKey", Select + " AND exchange=:exchange AND symbol=:symbol AND currency=:currency;")
            .SetParameters(new Parameters([(int)product.Family, product.Exchange, product.Symbol, product.Currency]))
            .ExecuteQueryAsync(x => Map(product.Family, x), cancellationToken).ConfigureAwait(false)).SingleOrDefault();
    public async Task<TradeStrategySymbolReadModel?> FindAsync(TradeStrategyFamilyType family, int id, CancellationToken cancellationToken) =>
        (await db.ReferenceDb.Use("TradeStrategySymbol.ByFamily", Select + ";")
            .SetParameters(new Parameters([(int)family]))
            .ExecuteQueryAsync(x => Map(family, x), cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.Id == id);
    static TradeStrategySymbolReadModel Map(TradeStrategyFamilyType family, IObjectDataRecord row) =>
        new TradeStrategyProduct(family, row.GetString(1), row.GetString(2), row.GetString(3)).WithId(row.GetInt(0));
    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
}

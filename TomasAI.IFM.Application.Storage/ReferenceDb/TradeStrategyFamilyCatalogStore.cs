using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public interface ITradeStrategyFamilyCatalogStore
{
    Task<TradeStrategyFamilyReadModel> CreateAsync(CreateTradeStrategyFamilyRequest request, TradeStrategyFamilyReadModel candidate, CancellationToken cancellationToken);
}

/// <summary>Small reference catalog: one CAS document atomically owns natural keys and operation receipts.</summary>
public sealed class TradeStrategyFamilyCatalogStore(IDbContextFactory db, ISequenceIdGenerator ids) : ITradeStrategyFamilyCatalogStore
{
    public const string CreateTable = "CREATE TABLE IF NOT EXISTS trade_strategy_family_catalog_v4 (catalog text PRIMARY KEY, revision bigint, payload_json text);";
    const string Select = "SELECT revision,payload_json FROM trade_strategy_family_catalog_v4 WHERE catalog=:catalog;";
    public sealed record Entry(Guid OperationId, CreateTradeStrategyFamilyRequest Request, TradeStrategyFamilyReadModel Definition);
    sealed record Snapshot(long Revision, Entry[] Entries);
    static async Task<Snapshot?> Read(IDbContextFactory db, CancellationToken cancellationToken) =>
        (await db.ReferenceDb.Use("TradeStrategyFamilyCatalog.Read", Select).SetParameters(new Parameters(["V1"]))
            .ExecuteQueryAsync(x => new Snapshot(x.GetLong(0), JsonSerializer.Deserialize<Entry[]>(x.GetString(1))
                ?? throw new InvalidOperationException("Invalid family catalog document.")), cancellationToken).ConfigureAwait(false)).SingleOrDefault();
    public static async Task<IReadOnlyList<TradeStrategyFamilyReadModel>> ReadDefinitionsAsync(IDbContextFactory db, CancellationToken cancellationToken) =>
        (await Read(db, cancellationToken).ConfigureAwait(false))?.Entries.Select(x => x.Definition).ToArray() ?? [];

    public async Task<TradeStrategyFamilyReadModel> CreateAsync(CreateTradeStrategyFamilyRequest request, TradeStrategyFamilyReadModel candidate, CancellationToken cancellationToken)
    {
        if (request.Validate().Count != 0 || candidate.TradeStrategySymbolId != request.TradeStrategySymbolId || candidate.Family != request.Family ||
            candidate.Strategy != request.Strategy || candidate.TimeFrame != request.TimeFrame || candidate.Description != request.Description.Trim())
            throw new ArgumentException("Invalid catalog creation request.");
        int? allocatedId = null;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await Read(db, cancellationToken).ConfigureAwait(false);
            var entries = snapshot?.Entries ?? [];
            var replay = entries.SingleOrDefault(x => x.OperationId == request.OperationId);
            if (replay is not null)
            {
                if (replay.Request != request) throw new InvalidOperationException("OperationId was already used with a different request.");
                return replay.Definition;
            }
            if (entries.Any(x => x.Definition.Family == candidate.Family && x.Definition.Strategy == candidate.Strategy &&
                x.Definition.TimeFrame == candidate.TimeFrame && x.Definition.TradeStrategySymbolId == candidate.TradeStrategySymbolId))
                throw new InvalidOperationException("This family/strategy/product/timeframe definition already exists.");
            if (entries.Length >= 1000) throw new InvalidOperationException("The family catalog limit of 1000 created entries has been reached.");
            allocatedId ??= checked((int)await ids.GetSequenceIdAsync(SequenceName.Reference_TradeStrategyFamilyId, cancellationToken).ConfigureAwait(false));
            var row = candidate with { TradeStrategyFamilyId = allocatedId.Value, DefinitionVersion = 1, State = TradeStrategyFamilyState.Active };
            if (row.Validate().Count != 0 || row.TradeStrategySymbolId <= 0) throw new ArgumentException("Invalid product-linked family definition.");
            var json = JsonSerializer.Serialize(entries.Append(new Entry(request.OperationId, request, row)).ToArray());
            if (snapshot is null)
                await db.ReferenceDb.Use("TradeStrategyFamilyCatalog.Initialize", "INSERT INTO trade_strategy_family_catalog_v4(catalog,revision,payload_json) VALUES(:catalog,:revision,:payload_json) IF NOT EXISTS;")
                    .SetParameters(new Parameters(["V1", 1L, json])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            else
                await db.ReferenceDb.Use("TradeStrategyFamilyCatalog.CompareExchange", "UPDATE trade_strategy_family_catalog_v4 SET revision=:revision,payload_json=:payload_json WHERE catalog=:catalog IF revision=:expected;")
                    .SetParameters(new Parameters([snapshot.Revision + 1, json, "V1", snapshot.Revision])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            // Read back the operation receipt. A losing initializer retries against the winning snapshot.
        }
        throw new InvalidOperationException("Family catalog was modified concurrently; retry the same OperationId.");
    }
    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
}

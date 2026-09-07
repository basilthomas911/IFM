// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public interface ITradeStrategyFamilyCatalogStore
{
    Task<TradeStrategyFamilyReadModel> CreateAsync(CreateTradeStrategyFamilyRequest request, TradeStrategyFamilyReadModel candidate, CancellationToken cancellationToken);
    Task<TradeStrategyFamilyReadModel> ChangeAsync(ChangeTradeStrategyFamilyRequest request, TradeStrategyFamilyReadModel candidate, CancellationToken cancellationToken);
    Task<TradeStrategyFamilyReadModel> RemoveAsync(RemoveTradeStrategyFamilyRequest request, DateTime removedUtc, string principal, CancellationToken cancellationToken);
}

/// <summary>Small reference catalog: one CAS document atomically owns natural keys and operation receipts.</summary>
public sealed class TradeStrategyFamilyCatalogStore(IDbContextFactory db, ISequenceIdGenerator ids) : ITradeStrategyFamilyCatalogStore
{
    public const string CreateTable = "CREATE TABLE IF NOT EXISTS trade_strategy_family_catalog_v4 (catalog text PRIMARY KEY, revision bigint, payload_json text);";
    const string Select = "SELECT revision,payload_json FROM trade_strategy_family_catalog_v4 WHERE catalog=:catalog;";
    public sealed record Entry(Guid OperationId, CreateTradeStrategyFamilyRequest? Request, TradeStrategyFamilyReadModel Definition,
        ChangeTradeStrategyFamilyRequest? Change = null, RemoveTradeStrategyFamilyRequest? Remove = null);
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
                if (replay.Change is not null || replay.Remove is not null || replay.Request != request) throw new InvalidOperationException("OperationId was already used with a different request.");
                return replay.Definition;
            }
            if (Current(entries.Select(x => x.Definition)).Any(x => SameProduct(x, candidate)))
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

    static IEnumerable<TradeStrategyFamilyReadModel> Current(IEnumerable<TradeStrategyFamilyReadModel> rows) => rows
        .GroupBy(x => x.TradeStrategyFamilyId).Select(x => x.MaxBy(v => v.DefinitionVersion)!).Where(x => x.State == TradeStrategyFamilyState.Active);
    static bool SameProduct(TradeStrategyFamilyReadModel x, TradeStrategyFamilyReadModel y) =>
        x.Family == y.Family && x.Strategy == y.Strategy && x.TimeFrame == y.TimeFrame && x.TradeStrategySymbolId == y.TradeStrategySymbolId;

    public Task<TradeStrategyFamilyReadModel> ChangeAsync(ChangeTradeStrategyFamilyRequest request, TradeStrategyFamilyReadModel candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate().Count != 0 || candidate.Family != request.Definition.Family || candidate.Strategy != request.Definition.Strategy ||
            candidate.TimeFrame != request.Definition.TimeFrame || candidate.TradeStrategySymbolId != request.Definition.TradeStrategySymbolId ||
            candidate.Description != request.Definition.Description.Trim()) throw new ArgumentException("Invalid catalog change request.");
        return MutateAsync(request.OperationId, request.Target, request, null, candidate, candidate.CreatedOnUtc, candidate.CreatedBy, cancellationToken);
    }
    public Task<TradeStrategyFamilyReadModel> RemoveAsync(RemoveTradeStrategyFamilyRequest request, DateTime removedUtc, string principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate().Count != 0) throw new ArgumentException("Invalid catalog removal request.");
        return MutateAsync(request.OperationId, request.Target, null, request, null, removedUtc, principal, cancellationToken);
    }

    async Task<TradeStrategyFamilyReadModel> MutateAsync(Guid operationId, TradeStrategyFamilyReference target,
        ChangeTradeStrategyFamilyRequest? change, RemoveTradeStrategyFamilyRequest? remove, TradeStrategyFamilyReadModel? candidate,
        DateTime auditUtc, string principal, CancellationToken cancellationToken)
    {
        if (auditUtc == default || auditUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(principal)) throw new ArgumentException("UTC audit provenance is required.");
        for (var attempt = 0; attempt < 16; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await Read(db, cancellationToken).ConfigureAwait(false);
            var entries = snapshot?.Entries ?? [];
            var replay = entries.SingleOrDefault(x => x.OperationId == operationId);
            if (replay is not null)
            {
                if (replay.Change != change || replay.Remove != remove) throw new InvalidOperationException("OperationId was already used with a different request.");
                return replay.Definition;
            }
            // Includes immutable seed definitions as well as all catalog versions. The CAS below
            // fences any concurrent catalog change after this snapshot was read.
            var rows = await db.ReferenceDb.GetTradeStrategyFamiliesAsync(cancellationToken).ConfigureAwait(false);
            // The aggregate read includes this document. Retry if it observed a newer revision
            // so a concurrent retry of the same operation finds its receipt before stale checks.
            if ((await Read(db, cancellationToken).ConfigureAwait(false))?.Revision != snapshot?.Revision) continue;
            var current = rows.Where(x => x.TradeStrategyFamilyId == target.TradeStrategyFamilyId).MaxBy(x => x.DefinitionVersion);
            if (current is null || current.DefinitionVersion != target.DefinitionVersion || current.State != TradeStrategyFamilyState.Active)
                throw new InvalidOperationException("The strategy has changed or was removed. Reload the catalog and try again.");
            var next = (candidate ?? current) with { TradeStrategyFamilyId = current.TradeStrategyFamilyId,
                DefinitionVersion = checked(current.DefinitionVersion + 1), State = remove is null ? TradeStrategyFamilyState.Active : TradeStrategyFamilyState.Retired,
                CreatedOnUtc = auditUtc, CreatedBy = principal };
            if (next.Validate().Count != 0) throw new ArgumentException("Invalid family definition.");
            if (remove is null && Current(rows).Any(x => x.TradeStrategyFamilyId != next.TradeStrategyFamilyId && SameProduct(x, next)))
                throw new InvalidOperationException("This family/strategy/product/timeframe definition already exists.");
            if (entries.Length >= 1000 && remove is null) throw new InvalidOperationException("The family catalog limit of 1000 entries has been reached.");
            var json = JsonSerializer.Serialize(entries.Append(new Entry(operationId, change?.Definition, next, change, remove)).ToArray());
            if (snapshot is null)
                await db.ReferenceDb.Use("TradeStrategyFamilyCatalog.Initialize", "INSERT INTO trade_strategy_family_catalog_v4(catalog,revision,payload_json) VALUES(:catalog,:revision,:payload_json) IF NOT EXISTS;")
                    .SetParameters(new Parameters(["V1", 1L, json])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            else
                await db.ReferenceDb.Use("TradeStrategyFamilyCatalog.CompareExchange", "UPDATE trade_strategy_family_catalog_v4 SET revision=:revision,payload_json=:payload_json WHERE catalog=:catalog IF revision=:expected;")
                    .SetParameters(new Parameters([snapshot.Revision + 1, json, "V1", snapshot.Revision])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Family catalog was modified concurrently; retry the same OperationId.");
    }
    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
}

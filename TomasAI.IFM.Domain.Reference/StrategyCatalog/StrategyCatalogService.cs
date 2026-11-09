using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.StrategyCatalog.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog;

/// <summary>Coordinates catalog source-event acceptance, durable projection, and projected reads.</summary>
public sealed class StrategyCatalogService(IDbContextFactory factory, IPostgresEventTransaction? transactions = null, TimeProvider? timeProvider = null)
{
    public async Task ExecuteEventSourcedAsync(CatalogCommandRequest request, string principal,
        IEventProjector<StrategyCatalogCommandActor> projector, ActorEntityId entityId, CancellationToken token)
    {
        var key = request.Operation == CatalogCommandOperation.SaveDraft ? request.Definition?.Key : request.Key;
        if (key is null || StrategyCatalogLifecycleCommand.Address(key) != entityId)
            throw new InvalidOperationException("Catalog command route does not match its version identity.");
        var unitOfWork = transactions ?? throw new InvalidOperationException("Catalog event persistence is unavailable.");
        var database = factory.ConfigurationDb;
        if (PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(factory.ActorEventSourceDb.ConnectionString))
            throw new InvalidOperationException("Strategy catalog and source events must share one PostgreSQL database/security identity.");
        var fact = await unitOfWork.ExecuteOnceAsync((transaction, cancellationToken) =>
            database.RecordStrategyCatalogTransitionAsync(transaction, request, principal, cancellationToken), token).ConfigureAwait(false);
        if (fact is null)
            return;
        await projector.ProcessDomainEventAsync(fact).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(new DomainEventCollection([fact])).ConfigureAwait(false);
    }

    public Task<bool> ProjectAsync(StrategyCatalogSourceEvent fact, bool rebuild, CancellationToken token)
        => (transactions ?? throw new InvalidOperationException("Catalog event persistence is unavailable."))
            .ExecuteOnceAsync((transaction, cancellationToken) =>
                factory.ConfigurationDb.ProjectStrategyCatalogSourceAsync(transaction, fact, rebuild, cancellationToken), token);

    public async Task<string> QueryAsync(CatalogQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var db = factory.ConfigurationDb;
        return request.Operation switch
        {
            CatalogQueryOperation.List => StrategyCatalogJson.Write(await (db.GetStrategyCatalogsAsync(request.Kind, request.Limit, request.AfterCode, ct)).ConfigureAwait(false)),
            CatalogQueryOperation.Exact when request.Key is not null => StrategyCatalogJson.Write(await (db.GetStrategyCatalogAsync(request.Key, ct)).ConfigureAwait(false)),
            CatalogQueryOperation.DeploymentChoices => StrategyCatalogJson.Write(await (DeploymentChoices(request, ct)).ConfigureAwait(false)),
            CatalogQueryOperation.ValidatePublishedDeployment when request.Key is not null =>
                StrategyCatalogJson.Write((await (db.GetPublishedStrategyDeploymentAsync(request.Key, new DateTime((timeProvider ?? TimeProvider.System).GetUtcNow().UtcTicks / 10 * 10, DateTimeKind.Utc), ct)).ConfigureAwait(false)).ContentHash),
            _ => throw new ArgumentException("Unsupported catalog query.",nameof(request))
        };
    }

    async Task<StrategyDeploymentPage> DeploymentChoices(CatalogQueryRequest request, CancellationToken ct)
    {
        var db = factory.ConfigurationDb;
        var rows = await (db.GetStrategyCatalogsAsync(StrategyCatalogKind.Deployment, Math.Min(64, request.Limit), request.AfterCode, ct)).ConfigureAwait(false);
        var result = new List<StrategyDeploymentChoice>();
        string? next = null;
        foreach (var row in rows)
        {
            var stored = await (db.GetStrategyCatalogAsync(row.Key, ct)).ConfigureAwait(false) ?? throw new InvalidOperationException("Deployment disappeared.");
            var definition = stored.Definition; var classes = new HashSet<string>(StringComparer.Ordinal); var variants = new List<string>();
            foreach (var key in definition.Variants)
            {
                var variant = await (db.GetStrategyCatalogAsync(key, ct)).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing exact variant.");
                variants.Add(variant.Definition.Name);
                var structure = await (db.GetStrategyCatalogAsync(variant.Definition.Parent!, ct)).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing exact structure.");
                foreach (var leg in structure.Definition.Legs) classes.Add(leg.InstrumentClass);
            }
            var choice = new StrategyDeploymentChoice(row.Key, row.Code, row.Name, row.Status, definition.Horizon, definition.Products,
                classes.Order(StringComparer.Ordinal).ToArray(), definition.PipelineParameters, variants.ToArray());
            if (System.Text.Encoding.UTF8.GetByteCount(StrategyCatalogJson.Write(result.Append(choice).ToArray())) > 450000)
            {
                if (result.Count == 0) throw new InvalidOperationException("Deployment exceeds the query message limit.");
                next = result[^1].Code; break;
            }
            result.Add(choice);
        }
        if (next is null && rows.Count == Math.Min(64, request.Limit)) next = result.LastOrDefault()?.Code;
        return new(result.ToArray(), next);
    }
}

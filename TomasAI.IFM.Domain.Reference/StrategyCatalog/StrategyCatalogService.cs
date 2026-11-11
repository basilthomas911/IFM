using TomasAI.IFM.Application.Strategy.StrategyCatalog;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
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
public sealed class StrategyCatalogService(IDbContextFactory factory, IPostgresEventTransaction? transactions = null, TimeProvider? timeProvider = null,
    StrategyCatalogQualification? qualification = null)
{
    public async Task ExecuteEventSourcedAsync(CatalogCommandRequest request, string principal,
        IEventProjector<StrategyCatalogCommandActor> projector, ActorEntityId entityId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = request.Operation == CatalogCommandOperation.SaveDraft ? request.Definition?.Key : request.Key;
        if (key is null || StrategyCatalogLifecycleCommand.Address(key) != entityId)
            throw new InvalidOperationException("Catalog command route does not match its version identity.");
        var unitOfWork = transactions ?? throw new InvalidOperationException("Catalog event persistence is unavailable.");
        var database = factory.ConfigurationDb;
        if (PostgresDatabaseIdentity.Parse(database.ConnectionString) != PostgresDatabaseIdentity.Parse(factory.ActorEventSourceDb.ConnectionString))
            throw new InvalidOperationException("Strategy catalog and source events must share one PostgreSQL database/security identity.");
        var fact = await CommitAsync(database, unitOfWork, request, principal, entityId, timeProvider, token, qualification)
            .ConfigureAwait(false);
        if (fact is null)
            return;
        await projector.ProcessDomainEventAsync(fact).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(new DomainEventCollection([fact])).ConfigureAwait(false);
    }

    /// <summary>Commits the owning actor's accepted catalog fact before durable projection can run.</summary>
    public static async Task<StrategyCatalogSourceEvent?> CommitAsync(IConfigurationDbContext database,
        IPostgresEventTransaction unitOfWork, CatalogCommandRequest request, string principal, ActorEntityId entityId,
        TimeProvider? timeProvider, CancellationToken token = default, StrategyCatalogQualification? qualification = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        var stream = $"StrategyCatalog.{entityId.Format()}";
        StrategyCatalogPublicationProof? publication = null;
        if (request.Operation == CatalogCommandOperation.Publish && request.Key is { } key
            && request.ExpectedHash is { } hash && request.EffectiveUtc is { } effective)
        {
            var receipt = await database.ReadCommittedStrategyCatalogOperationAsync(stream, request.OperationId, token)
                .ConfigureAwait(false);
            if (receipt is not null)
            {
                if (!string.Equals(receipt.RequestJson, StrategyCatalogJson.Write(request), StringComparison.Ordinal)
                    || !string.Equals(receipt.Principal, principal, StringComparison.Ordinal))
                    throw new InvalidOperationException("Catalog operation identity was reused with different input.");
                return null;
            }
            var previous = await database.GetStrategyCatalogAsync(key, token).ConfigureAwait(false);
            if (!(previous is { Status: CatalogLifecycleStatus.Published } && previous.EffectiveFromUtc == effective
                  && string.Equals(previous.ContentHash, hash, StringComparison.Ordinal) && string.Equals(previous.PublishedBy, principal, StringComparison.Ordinal)))
                publication = await (qualification ?? new StrategyCatalogQualification()).PreparePublicationAsync(database, key, hash, effective, token)
                    .ConfigureAwait(false);
        }
        return await unitOfWork.ExecuteOnceAsync<StrategyCatalogSourceEvent?>(async (transaction, cancellationToken) =>
        {
            var prepared = await database.PrepareStrategyCatalogTransitionAsync(
                transaction, request, principal, stream, publication, cancellationToken).ConfigureAwait(false);
            if (prepared is null) return null;
            var source = new StrategyCatalogSourceEvent
            {
                Subject = new ActorSubject(ActorType.Event, "StrategyCatalogEvent", request.Operation.ToString(), entityId.Format()),
                EntityId = entityId, Id = request.OperationId, CommandId = request.OperationId,
                AggregateId = entityId.Format(), EventSource = "StrategyCatalogCommandActor",
                ReceivedOn = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime,
                RequestJson = prepared.RequestJson, BeforeJson = prepared.BeforeJson, AfterJson = prepared.AfterJson,
                Principal = principal
            };
            var eventId = await transaction.AppendAsync(new EnlistedEventAppend(stream, request.OperationId, source, prepared.StreamVersion), cancellationToken)
                .ConfigureAwait(false);
            await database.MarkParameterProjectionPendingAsync(transaction, eventId, cancellationToken).ConfigureAwait(false);
            await database.AdvanceParameterRevisionFenceAsync(transaction, cancellationToken).ConfigureAwait(false);
            return source;
        }, token).ConfigureAwait(false);
    }

    public Task<bool> ProjectAsync(StrategyCatalogSourceEvent fact, bool rebuild, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return (transactions ?? throw new InvalidOperationException("Catalog event persistence is unavailable."))
            .ExecuteOnceAsync((transaction, cancellationToken) =>
                factory.ConfigurationDb.ProjectStrategyCatalogSourceAsync(transaction, fact, rebuild, cancellationToken), token);
    }

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
                StrategyCatalogJson.Write((await ((qualification ?? new StrategyCatalogQualification()).ReadPublishedAsync(db, request.Key, new DateTime((timeProvider ?? TimeProvider.System).GetUtcNow().UtcTicks / 10 * 10, DateTimeKind.Utc), ct)).ConfigureAwait(false)).ContentHash),
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

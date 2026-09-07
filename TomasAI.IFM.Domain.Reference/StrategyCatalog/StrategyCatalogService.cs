using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog;

/// <summary>Reference-facing catalog application service. ConfigurationDb is the sole write authority.</summary>
public sealed class StrategyCatalogService(IDbContextFactory factory)
{
    public async Task<string> QueryAsync(CatalogQueryRequest request, CancellationToken ct = default)
    {
        var db = factory.ConfigurationDb;
        return request.Operation switch
        {
            CatalogQueryOperation.List => StrategyCatalogJson.Write(await db.ListStrategyCatalogAsync(request.Kind, request.Limit, request.AfterCode, ct)),
            CatalogQueryOperation.Exact when request.Key is not null => StrategyCatalogJson.Write(await db.GetStrategyCatalogAsync(request.Key, ct)),
            CatalogQueryOperation.DeploymentChoices => StrategyCatalogJson.Write(await DeploymentChoices(request, ct)),
            CatalogQueryOperation.ValidatePublishedDeployment when request.Key is not null =>
                StrategyCatalogJson.Write((await db.GetPublishedStrategyDeploymentAsync(request.Key, new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc), ct)).ContentHash),
            _ => throw new ArgumentException("Unsupported catalog query.")
        };
    }

    async Task<StrategyDeploymentPage> DeploymentChoices(CatalogQueryRequest request, CancellationToken ct)
    {
        var db = factory.ConfigurationDb;
        var rows = await db.ListStrategyCatalogAsync(StrategyCatalogKind.Deployment, Math.Min(64, request.Limit), request.AfterCode, ct);
        var result = new List<StrategyDeploymentChoice>();
        string? next = null;
        foreach (var row in rows)
        {
            var stored = await db.GetStrategyCatalogAsync(row.Key, ct) ?? throw new InvalidOperationException("Deployment disappeared.");
            var definition = stored.Definition; var classes = new HashSet<string>(StringComparer.Ordinal); var variants = new List<string>();
            foreach (var key in definition.Variants)
            {
                var variant = await db.GetStrategyCatalogAsync(key, ct) ?? throw new InvalidOperationException("Missing exact variant.");
                variants.Add(variant.Definition.Name);
                var structure = await db.GetStrategyCatalogAsync(variant.Definition.Parent!, ct) ?? throw new InvalidOperationException("Missing exact structure.");
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

    public async Task ExecuteAsync(CatalogCommandRequest request, string principal, CancellationToken ct = default)
    {
        if (request.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(principal)) throw new ArgumentException("Catalog operation identity and principal are required.");
        var db = factory.ConfigurationDb;
        switch (request.Operation)
        {
            case CatalogCommandOperation.SaveDraft when request.Definition is not null:
                var draft = StrategyCatalogValidation.Freeze(request.Definition);
                var previous = await db.GetStrategyCatalogAsync(draft.Key, ct);
                if (previous is not null)
                {
                    if (previous.ContentHash == StrategyCatalogValidation.ContentHash(draft) && previous.CreatedBy == principal && draft.Key.Version == request.ExpectedPreviousVersion + 1) return;
                    throw new InvalidOperationException("This catalog version already exists with different content. Reload before editing.");
                }
                await db.InsertStrategyCatalogDraftAsync(draft, request.ExpectedPreviousVersion, principal, ct);
                break;
            case CatalogCommandOperation.Publish when request.Key is not null && request.ExpectedHash is not null && request.EffectiveUtc is not null:
                var published = await db.GetStrategyCatalogAsync(request.Key, ct);
                if (published is { Status: CatalogLifecycleStatus.Published } && published.ContentHash == request.ExpectedHash && published.EffectiveFromUtc == request.EffectiveUtc && published.PublishedBy == principal) return;
                await db.PublishStrategyCatalogAsync(request.Key, request.ExpectedHash, request.EffectiveUtc.Value, principal, ct);
                break;
            case CatalogCommandOperation.Retire when request.Key is not null && request.ExpectedHash is not null && request.EffectiveUtc is not null:
                var retired = await db.GetStrategyCatalogAsync(request.Key, ct);
                if (retired is { Status: CatalogLifecycleStatus.Retired } && retired.ContentHash == request.ExpectedHash && retired.RetiredAtUtc == request.EffectiveUtc && retired.RetiredBy == principal) return;
                await db.RetireStrategyCatalogAsync(request.Key, request.ExpectedHash, request.EffectiveUtc.Value, principal, ct);
                break;
            default: throw new ArgumentException("Incomplete or unsupported catalog command.");
        }
    }
}

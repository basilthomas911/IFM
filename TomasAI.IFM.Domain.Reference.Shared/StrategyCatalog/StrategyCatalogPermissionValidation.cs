using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

public static class StrategyCatalogPermissionValidation
{
    public static async Task<StoredStrategyCatalogDefinition> ValidateDeploymentAsync(IReferenceQueryApi queries, CatalogKey key,
        bool requirePublished, CancellationToken ct = default)
    {
        if (key.Kind != StrategyCatalogKind.Deployment || key.Id == Guid.Empty || key.Version <= 0) throw new ArgumentException("Invalid deployment reference.");
        var reply = await queries.QueryStrategyCatalogAsync(new(CatalogQueryOperation.Exact, Key: key), ct);
        if (!reply.Success || reply.Value is null || reply.Value == "null") throw new InvalidOperationException(reply.ErrorMessage ?? "Exact deployment is missing.");
        var row = StrategyCatalogJson.Read<StoredStrategyCatalogDefinition>(reply.Value);
        if (row.Definition.Key != key || row.Status == CatalogLifecycleStatus.Retired) throw new InvalidOperationException("Exact deployment is missing or retired.");
        if (requirePublished)
        {
            if (row.Status != CatalogLifecycleStatus.Published) throw new InvalidOperationException("Deployment must be Published before workflow use.");
            var published = await queries.QueryStrategyCatalogAsync(new(CatalogQueryOperation.ValidatePublishedDeployment, Key: key), ct);
            if (!published.Success || published.Value is null || StrategyCatalogJson.Read<string>(published.Value).Length != 64) throw new InvalidOperationException(published.ErrorMessage ?? "Deployment is not qualified and Published.");
        }
        return row;
    }
}

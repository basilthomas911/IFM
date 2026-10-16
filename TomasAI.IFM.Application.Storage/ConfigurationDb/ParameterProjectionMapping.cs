using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Relational version metadata. Concrete values live in the parameter value tables.</summary>
[MessagePackObject(AllowPrivate=true)]
internal sealed record ParameterVersionMetadata(
    [property:Key(0)] ParameterVersionRef Reference,[property:Key(1)] string Name,[property:Key(2)] string Description,
    [property:Key(3)] int SchemaVersion,[property:Key(4)] ParameterVersionStatus Status,
    [property:Key(5)] DateTime CreatedAtUtc,[property:Key(6)] string CreatedBy,
    [property:Key(7)] DateTime? PublishedAtUtc,[property:Key(8)] DateTime? RetiredAtUtc,
    [property:Key(9)] long CatalogRevision,[property:Key(10)] ParameterLegacyReference? LegacySource,
    [property:Key(11)] string ReferenceCodec)
{
    internal static ParameterVersionMetadata From(ParameterSetVersion value)=>new(value.Reference,value.Name,value.Description,value.SchemaVersion,
        value.Status,value.CreatedAtUtc,value.CreatedBy,value.PublishedAtUtc,value.RetiredAtUtc,value.CatalogRevision,value.LegacySource,
        value.Parameters is null ? "sorted-json-v1" : value.ReferenceCodec);
    internal ParameterSetVersion Restore(ParameterSetValue values)=>new(Reference,Name,Description,SchemaVersion,Status,string.Empty,CreatedAtUtc,
        CreatedBy,PublishedAtUtc,RetiredAtUtc,CatalogRevision,LegacySource){Parameters=values,ReferenceCodec=ReferenceCodec};
}
[MessagePackObject(AllowPrivate=true)]
internal sealed record ParameterStartupProjection(
    [property:Key(0)] Guid RunId,[property:Key(1)] ParameterAssignmentRevision[] Scopes,
    [property:Key(2)] ParameterVersionMetadata[] Versions,[property:Key(3)] ParameterSignalStartupPlan Plan,
    [property:Key(4)] DateTime CreatedAtUtc,[property:Key(5)] string CreatedBy)
{
    internal static ParameterStartupProjection From(ParameterStartupRun run)=>new(run.RunId,run.Scopes,run.Versions.Select(ParameterVersionMetadata.From).ToArray(),run.Plan,run.CreatedAtUtc,run.CreatedBy);
}

internal static class ParameterProjectionMapping
{
    internal static readonly IReadOnlyDictionary<Type,ParameterValueMapping.Table> Roots=new Dictionary<Type,ParameterValueMapping.Table>
    {
        [typeof(ParameterVersionMetadata)]=ParameterValueMapping.Build(typeof(ParameterVersionMetadata),null,"parameter_catalog_version"),
        [typeof(ParameterAuditEntry)]=ParameterValueMapping.Build(typeof(ParameterAuditEntry),null,"parameter_catalog_audit"),
        [typeof(ParameterAssignmentRevision)]=ParameterValueMapping.Build(typeof(ParameterAssignmentRevision),null,"parameter_catalog_assignment"),
        [typeof(ParameterStartupProjection)]=ParameterValueMapping.Build(typeof(ParameterStartupProjection),null,"parameter_catalog_startup"),
        [typeof(ParameterSignalStartupReport)]=ParameterValueMapping.Build(typeof(ParameterSignalStartupReport),null,"parameter_catalog_startup_report")
    };
    internal static IEnumerable<ParameterValueMapping.Table> Tables=>Roots.Values.SelectMany(ParameterValueMapping.Descendants);
    internal static IReadOnlyList<ParameterValueMapping.Row> Write<T>(T value) where T:notnull
    {
        var rows=new List<ParameterValueMapping.Row>();ParameterValueMapping.Walk(Roots[typeof(T)],value,"$",null,0,rows);return rows;
    }
    internal static T Read<T>(IReadOnlyList<ParameterValueMapping.Row> rows)=>
        (T)ParameterValueMapping.Materialize(Roots[typeof(T)],rows.Single(x=>x.Table==Roots[typeof(T)]&&x.Path=="$"),rows);
}

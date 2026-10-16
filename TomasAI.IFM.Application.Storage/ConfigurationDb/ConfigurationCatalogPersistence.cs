using MessagePack;
using System.Security.Cryptography;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Bound relational catalog materialization. Runtime orchestration remains in domain repositories.</summary>
internal static class ConfigurationCatalogPersistence
{
    static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(MessagePackSerializer.Serialize(value,MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None))));

    internal static async Task WriteAsync<T>(this ConfigurationDbContext context,IEnlistedPostgresTransaction tx,Guid id,long version,T value,
        bool replace,CancellationToken token) where T:class
    {
        var root=ParameterProjectionMapping.Roots[typeof(T)];var hash=Hash(value);
        var previous=await tx.ScalarAsync(ConfigurationDbSql.TypedCatalog.Header,[root.Name,id,version],token).ConfigureAwait(false);
        if(previous is string existing)
        {
            if(existing==hash) { await context.ReadCatalogAsync<T>(tx,id,version,token).ConfigureAwait(false);return; }
            if(!replace) throw new InvalidDataException("PARAM.IMMUTABLE_CONTENT_CONFLICT");
            foreach(var table in ParameterValueMapping.Descendants(root).Reverse())
                await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.Delete(table),[id,version],token).ConfigureAwait(false);
        }
        await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.PutHeader,[root.Name,id,version,hash],token).ConfigureAwait(false);
        foreach(var row in ParameterProjectionMapping.Write(value))
            await tx.ExecuteAsync(ConfigurationDbSql.TypedParameters.Insert(row.Table),[id,version,row.Path,row.ParentPath,row.Ordinal,..row.Values],token).ConfigureAwait(false);
    }

    internal static async Task<T?> ReadCatalogAsync<T>(this ConfigurationDbContext context,IEnlistedPostgresTransaction tx,Guid id,long version,CancellationToken token) where T:class
    {
        var root=ParameterProjectionMapping.Roots[typeof(T)];
        var hash=await tx.ScalarAsync(ConfigurationDbSql.TypedCatalog.Header,[root.Name,id,version],token).ConfigureAwait(false);
        if(hash is null)return null;
        var rows=new List<ParameterValueMapping.Row>();
        foreach(var table in ParameterValueMapping.Descendants(root))
        {
            var batch=await tx.QueryAsync(ConfigurationDbSql.TypedParameters.Read(table),[id,version],reader=>new ParameterValueMapping.Row(table,
                reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1),reader.GetInt32(2),
                Enumerable.Range(0,table.Columns.Count).Select(i=>reader.IsDBNull(i+3)?null:reader.GetValue(i+3)).ToArray()),token).ConfigureAwait(false);
            rows.AddRange(batch);
        }
        var result=ParameterProjectionMapping.Read<T>(rows);
        if(Hash(result)!=(string)hash)throw new InvalidDataException("PARAM.PROJECTION_CONTENT_MISMATCH");
        return result;
    }

    static async Task<bool> DuplicateAsync(IEnlistedPostgresTransaction tx,string kind,Guid entity,long revision,Guid operation,string request,string factHash,CancellationToken token)
    {
        await tx.ExecuteAsync(ConfigurationDbSql.TypedParameters.Lock,[kind+":"+entity.ToString("D")],token).ConfigureAwait(false);
        var receipts=await tx.QueryAsync(ConfigurationDbSql.TypedCatalog.Receipt,[operation],r=>(Request:r.GetString(0),Fact:r.GetString(1)),token).ConfigureAwait(false);
        if(receipts.Count!=0)
        {
            if(receipts[0].Request!=request||receipts[0].Fact!=factHash) throw new InvalidDataException("PARAM.OPERATION_IDENTITY_MISMATCH");
            return true;
        }
        var prior=Convert.ToInt64(await tx.ScalarAsync(ConfigurationDbSql.TypedCatalog.Revision,[kind,entity],token).ConfigureAwait(false));
        if(prior+1!=revision)throw new InvalidDataException("PARAM.PROJECTION_ORDER");
        return false;
    }
    static Task<int> Receipt(IEnlistedPostgresTransaction tx,string kind,Guid entity,long revision,Guid operation,string request,int version,DateTime at,string factHash,CancellationToken token)=>
        tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.InsertReceipt,[operation,entity,revision,request,kind,version,at.Ticks,factHash],token);

    internal static async Task ProjectCatalogSetAsync(this ConfigurationDbContext context,IEnlistedPostgresTransaction tx,IParameterSetFact fact,CancellationToken token)
    {
        var value=fact.ReadVersion();var audit=fact.ReadAudit();var factHash=Hash((fact.EventName,fact.Revision,value,audit));
        if(await DuplicateAsync(tx,"set",fact.EntityId.SetId,fact.Revision,fact.CommandId,fact.RequestHash,factHash,token).ConfigureAwait(false))return;
        var parameters=value.Values();
        if(parameters.SetId!=fact.EntityId.SetId||parameters.Version!=value.Reference.Version||parameters.SchemaVersion!=value.SchemaVersion||parameters.ComponentCode!=value.Reference.ComponentCode)
            throw new InvalidDataException("PARAM.REFERENCE_INVALID");
        if(value.Parameters is not null && value.ReferenceCodec==ParameterValueCodec.Codec && ParameterValueCodec.Hash(parameters)!=value.Reference.PayloadSha256)
            throw new InvalidDataException("PARAM.HASH_MISMATCH");
        var old=await context.ReadCatalogAsync<ParameterVersionMetadata>(tx,parameters.SetId,parameters.Version,token).ConfigureAwait(false);
        if(old is not null)
        {
            if(old.Reference!=value.Reference||old.SchemaVersion!=value.SchemaVersion||old.CreatedAtUtc!=value.CreatedAtUtc||old.CreatedBy!=value.CreatedBy||old.LegacySource!=value.LegacySource)
                throw new InvalidDataException("PARAM.VERSION_IMMUTABLE");
            if(old.Status!=value.Status && !((old.Status==ParameterVersionStatus.Draft&&value.Status==ParameterVersionStatus.Published)||(old.Status==ParameterVersionStatus.Published&&value.Status==ParameterVersionStatus.Retired)))
                throw new InvalidDataException("PARAM.LIFECYCLE_INVALID");
        }
        await context.InsertTypedParameterValueAsync(tx,parameters,token).ConfigureAwait(false);
        await tx.ExecuteAsync(ConfigurationDbSql.UpsertParameterSet,[parameters.SetId,parameters.ComponentCode,value.Name,value.Description,fact.Revision],token).ConfigureAwait(false);
        await context.WriteAsync(tx,parameters.SetId,parameters.Version,ParameterVersionMetadata.From(value),true,token).ConfigureAwait(false);
        if(audit is not null)
        {
            if(audit.OperationId!=fact.CommandId||audit.Revision!=fact.Revision)throw new InvalidDataException("Audit identity mismatch");
            await context.WriteAsync(tx,audit.OperationId,1,audit,false,token).ConfigureAwait(false);
        }
        if(value.Reference.Version==1 && value.LegacySource is {} source)
        {
            var legacy=new LegacyParameterReference(source.Kind,source.SetId,source.Version,value.Reference.SetId,value.Reference.Version,source.PayloadSha256,source.Codec);
            await tx.ExecuteAsync(ConfigurationDbSql.InsertLegacyParameterReference,context.BindProjection(legacy),token).ConfigureAwait(false);
            var same=await tx.ScalarAsync(ConfigurationDbSql.IsLegacyParameterReferenceEquivalent,
                [source.Kind,source.SetId,source.Version,value.Reference.SetId,source.PayloadSha256,source.Codec],token).ConfigureAwait(false);
            if(same is not true)throw new InvalidDataException("PARAM.LEGACY_MAPPING_CONFLICT");
        }
        await Receipt(tx,"set",parameters.SetId,fact.Revision,fact.CommandId,fact.RequestHash,parameters.Version,value.CreatedAtUtc,factHash,token).ConfigureAwait(false);
    }

    internal static async Task ProjectCatalogAssignmentAsync(this ConfigurationDbContext context,IEnlistedPostgresTransaction tx,ParameterAssignmentChangedEvent fact,CancellationToken token)
    {
        var value=fact.ReadAssignment();var audit=fact.ReadAudit();var factHash=Hash((value,audit));
        if(value.AssignmentId!=fact.EntityId.AssignmentId||value.Revision!=fact.Revision)throw new InvalidDataException("PARAM.REFERENCE_INVALID");
        if(await DuplicateAsync(tx,"assignment",value.AssignmentId,fact.Revision,fact.CommandId,fact.RequestHash,factHash,token).ConfigureAwait(false))return;
        await context.WriteAsync(tx,value.AssignmentId,value.Revision,value,false,token).ConfigureAwait(false);
        if(audit is not null) await context.WriteAsync(tx,audit.OperationId,1,audit,false,token).ConfigureAwait(false);
        await Receipt(tx,"assignment",value.AssignmentId,fact.Revision,fact.CommandId,fact.RequestHash,value.Reference.Version,value.CreatedAtUtc,factHash,token).ConfigureAwait(false);
    }

    internal static async Task ProjectCatalogStartupAsync(this ConfigurationDbContext context,IEnlistedPostgresTransaction tx,ParameterStartupChangedEvent fact,CancellationToken token)
    {
        var run=fact.ReadRun();var report=fact.ReadReport();var factHash=Hash((fact.RunId,fact.Released,run,report));
        if(await DuplicateAsync(tx,"startup",Guid.Empty,fact.Revision,fact.CommandId,factHash,factHash,token).ConfigureAwait(false))return;
        if(run is not null)
        {
            if(run.RunId!=fact.RunId||run.Plan.StartupRunId!=fact.RunId)throw new InvalidDataException("PARAM.STARTUP_INVALID");
            foreach(var version in run.Versions)
            {
                var values=version.Values();
                if(values.SetId!=version.Reference.SetId||values.Version!=version.Reference.Version||values.ComponentCode!=version.Reference.ComponentCode||
                    values.SchemaVersion!=version.SchemaVersion||version.ReferenceCodec==ParameterValueCodec.Codec&&ParameterValueCodec.Hash(values)!=version.Reference.PayloadSha256)
                    throw new InvalidDataException("PARAM.STARTUP_VERSION_INVALID");
                await context.InsertTypedParameterValueAsync(tx,values,token).ConfigureAwait(false);
            }
            await context.WriteAsync(tx,fact.RunId,1,ParameterStartupProjection.From(run),false,token).ConfigureAwait(false);
            await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.StartupState,[fact.RunId,fact.Revision,true],token).ConfigureAwait(false);
        }
        else if(report is not null)
        {
            if(report.RunId!=fact.RunId)throw new InvalidDataException("PARAM.STARTUP_REPORT_INVALID");
            await context.WriteAsync(tx,fact.RunId,1,report,false,token).ConfigureAwait(false);
        }
        else if(fact.Released)await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.StartupState,[fact.RunId,fact.Revision,false],token).ConfigureAwait(false);
        else throw new InvalidDataException("PARAM.STARTUP_INVALID");
        await Receipt(tx,"startup",Guid.Empty,fact.Revision,fact.CommandId,factHash,0,fact.ReceivedOn,factHash,token).ConfigureAwait(false);
    }
}

using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Migration;

/// <summary>Incrementally replays accepted parameter facts into the typed catalog before command intake starts.</summary>
public sealed class ParameterCatalogMigrationService(IPostgresEventTransaction transactions,IDbContextFactory databases)
{
    /// <summary>Preserves immutable source history; source facts and existing references are never rewritten.</summary>
    public Task<int> ExecuteAsync(CancellationToken token=default)=>transactions.ExecuteOnceAsync(async(tx,cancellationToken)=>
    {
        if(await tx.ScalarAsync("SELECT to_regclass('public.event_log')::text;",[],cancellationToken).ConfigureAwait(false) is null)return 0;
        await tx.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended('parameter-catalog-typed-migration',0));",[],cancellationToken).ConfigureAwait(false);
        var cut=Convert.ToInt64(await tx.ScalarAsync("SELECT COALESCE(max(eventversion),0) FROM public.event_log;",[],cancellationToken).ConfigureAwait(false));
        var cursor=Convert.ToInt64(await tx.ScalarAsync("SELECT source_cut FROM reference_configuration.parameter_catalog_migration WHERE migration_id=1;",[],cancellationToken).ConfigureAwait(false));
        var count=0;
        while(cursor<cut)
        {
            var events=await tx.QueryAsync("""
                SELECT n.eventtypename,l.eventversion,l.eventpayload FROM public.event_log l JOIN public.event_name_id n USING(eventnameid)
                WHERE l.eventversion>$1 AND l.eventversion<=$2 AND
                  (n.eventtypename LIKE '%ParameterSetCreatedEvent%' OR n.eventtypename LIKE '%ParameterDraftSavedEvent%'
                   OR n.eventtypename LIKE '%ParameterSetRenamedEvent%' OR n.eventtypename LIKE '%ParameterVersionPublishedEvent%'
                   OR n.eventtypename LIKE '%ParameterVersionRetiredEvent%' OR n.eventtypename LIKE '%ParameterAssignmentChangedEvent%'
                   OR n.eventtypename LIKE '%ParameterStartupChangedEvent%')
                ORDER BY l.eventversion LIMIT 100;
                """,[cursor,cut],reader=>new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel
                {EventTypeName=reader.GetString(0),EventVersion=reader.GetInt64(1),EventData=reader.GetFieldValue<byte[]>(2)},cancellationToken).ConfigureAwait(false);
            if(events.Count==0)break;
            foreach(var row in events)
            {
                var fact=row.ToDomainEvent();
                if(fact is IParameterSetFact or ParameterAssignmentChangedEvent or ParameterStartupChangedEvent)
                {
                    await ParameterAcceptedProjection.ApplyAsync(databases,tx,fact,cancellationToken).ConfigureAwait(false);count++;
                }
                cursor=row.EventVersion;
            }
        }
        // The former projection may also be covered by an older configuration baseline.
        // Never report a successful cutover if an existing version has no replayed typed equivalent.
        if(await tx.ScalarAsync("SELECT to_regclass('reference_configuration.parameter_set_version')::text;",[],cancellationToken).ConfigureAwait(false) is not null)
        {
            var historical=await tx.QueryAsync("SELECT body::text FROM reference_configuration.parameter_set_version ORDER BY set_id,version;",[],
                reader=>System.Text.Json.JsonSerializer.Deserialize<ParameterSetVersion>(reader.GetString(0))??throw new InvalidDataException("PARAM.LEGACY_VERSION_MISSING"),cancellationToken).ConfigureAwait(false);
            foreach(var version in historical)
            {
                var expected=version.Parameters??ParameterLegacyUpcaster.Verify(version).Parameters;
                var restored=await databases.ConfigurationDb.GetTypedParameterValueAsync(tx,version.Reference.SetId,version.Reference.Version,cancellationToken).ConfigureAwait(false);
                if(restored is null||ParameterValueCodec.Hash(restored)!=ParameterValueCodec.Hash(expected))
                    throw new InvalidDataException($"PARAM.MIGRATION_SOURCE_COVERAGE_MISSING: {version.Reference.SetId}/v{version.Reference.Version}");
            }
        }
        await tx.ExecuteAsync("INSERT INTO reference_configuration.parameter_catalog_migration(migration_id,source_cut) VALUES(1,$1) ON CONFLICT(migration_id) DO UPDATE SET source_cut=EXCLUDED.source_cut;",[cut],cancellationToken).ConfigureAwait(false);
        return count;
    },token);
}

using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Rebuild;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Storage.Tools;

/// <summary>Verifies actual persisted baseline facts in an explicitly isolated local database.</summary>
internal static class IsolatedConfigurationVerifier
{
    public static async Task RunAsync(ConfigurationProjectionBaselineEvent source)
    {
        var connection = Environment.GetEnvironmentVariable("IFM_CONFIGURATION_BASELINE_VERIFY_TARGET")
            ?? throw new InvalidOperationException("Set IFM_CONFIGURATION_BASELINE_VERIFY_TARGET to an empty isolated database.");
        var identity = new NpgsqlConnectionStringBuilder(connection);
        if (identity.Host is not ("localhost" or "127.0.0.1") ||
            !identity.Database.StartsWith("ifm_configuration_rebuild_qualification_", StringComparison.Ordinal))
            throw new InvalidOperationException("Rebuild verification requires the isolated local qualification database prefix.");
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres")
            .Add(ConfigurationDbContext.ConfigurationDbConnection, connection, "System.Data.Postgres");
        await ConfigurationSchemaDb.WithoutLookupDefaults(settings, NullLogger<DbProvider>.Instance)
            .CreateAllAsync().ConfigureAwait(false);
        var transactions = new PostgresEventTransaction(settings);
        await transactions.ExecuteOnceAsync(async (db, token) =>
        {
            await db.ExecuteAsync(MarketDataSchemaSql.OptionChainPoliciesCreate, [], token).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
        var baseline = new ConfigurationProjectionBaseline(transactions);
        await baseline.MaterializeAsync(source).ConfigureAwait(false);
        await baseline.MaterializeAsync(source).ConfigureAwait(false);
        await transactions.ExecuteOnceAsync(async (db, token) =>
        {
            foreach (var image in source.Tables)
            {
                // Table names come from the validated baseline catalog, never user input.
                var table = image.Name.Contains('.', StringComparison.Ordinal) ? image.Name : "reference_configuration." + image.Name;
                var rows = await db.ScalarAsync("SELECT COALESCE(jsonb_agg(j ORDER BY j::text),'[]'::jsonb)::text FROM (SELECT to_jsonb(t) j FROM " + table + " t) q;", [], token).ConfigureAwait(false);
                if (!string.Equals(rows as string, image.RowsJson, StringComparison.Ordinal))
                    throw new InvalidDataException("Isolated baseline reconstruction differs: " + image.Name);
            }
            var sequence = (await db.QueryAsync("SELECT last_value,is_called FROM reference_configuration.lookup_definition_id_seq;", [],
                reader => (Last: reader.GetInt64(0), Called: reader.GetBoolean(1)), token).ConfigureAwait(false)).Single();
            if (sequence.Last != source.LookupSequenceLastValue || sequence.Called != source.LookupSequenceIsCalled)
                throw new InvalidDataException("Isolated reconstruction changed the lookup sequence.");
            var completion = await db.ScalarAsync("SELECT source_event_id FROM reference_configuration.configuration_baseline_application WHERE baseline_id=$1;", [source.Id], token).ConfigureAwait(false);
            if (Convert.ToInt64(completion, CultureInfo.InvariantCulture) != source.EventId)
                throw new InvalidDataException("Isolated reconstruction completion differs.");
            return true;
        }).ConfigureAwait(false);
    }
}

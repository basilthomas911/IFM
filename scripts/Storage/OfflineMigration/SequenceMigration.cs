using System.Globalization;
using Npgsql;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Storage.Tools;

/// <summary>Preserves PostgreSQL allocation high-water marks while writers are stopped.
/// PostgreSQL setval is not rolled back; repeat verification before admitting any staging writers.</summary>
internal static class SequenceMigration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "PostgreSQL unquoted sequence identifiers are stored in lowercase; uppercase would select a different object.")]
    public static async Task<object> RunAsync(string connection)
    {
        var targetIdentity = new NpgsqlConnectionStringBuilder(connection);
        if (targetIdentity.Host is not ("localhost" or "127.0.0.1") || !targetIdentity.Database.StartsWith("ifm_storage_migration_staging_", StringComparison.Ordinal))
            throw new InvalidOperationException("Sequence migration requires an isolated local staging database.");
        var sourceConnection = SourceConnection();
        var provider = new PostgresObjectDataRepositoryConnection();
        var source = provider.As<NpgsqlConnection>(sourceConnection);
        await using var sourceDisposal = source.ConfigureAwait(false);
        var target = provider.As<NpgsqlConnection>(connection);
        await using var targetDisposal = target.ConfigureAwait(false);
        await source.OpenAsync().ConfigureAwait(false);
        await target.OpenAsync().ConfigureAwait(false);
        var sourceNames = await ReadNamesAsync(source).ConfigureAwait(false);
        var facts = new List<SequenceState>();
        var newNames = new List<string>();
        foreach (var name in Enum.GetNames<SequenceName>())
        {
            if (sourceNames.Contains(name.ToLowerInvariant())) facts.Add(await ReadAsync(source, name).ConfigureAwait(false));
            else
            {
                var fresh = await ReadAsync(target, name).ConfigureAwait(false);
                if (fresh.Called || fresh.Last != 1 || fresh.Increment != SequenceIdSettings.AllocationSize)
                    throw new InvalidDataException("A source-absent sequence is already allocated in the target: " + name);
                newNames.Add(name);
            }
        }
        // Preflight every high-water mark before setval: PostgreSQL sequence changes are not transactional.
        foreach (var fact in facts)
        {
            var current = await ReadAsync(target, fact.Name).ConfigureAwait(false);
            if (current.Increment != fact.Increment || fact.Increment != SequenceIdSettings.AllocationSize ||
                (current.Called && (current.Last > fact.Last || !fact.Called)))
                throw new InvalidDataException("Target sequence cannot safely receive source state: " + fact.Name);
        }
        var transaction = await target.BeginTransactionAsync().ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        foreach (var fact in facts)
        {
            using (var write = new NpgsqlCommand("SELECT setval($1::regclass,$2,$3);", target, transaction))
            {
                write.Parameters.AddWithValue("public." + fact.Name.ToLowerInvariant());
                write.Parameters.AddWithValue(fact.Last); write.Parameters.AddWithValue(fact.Called);
                await write.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            if (await ReadAsync(target, fact.Name, transaction).ConfigureAwait(false) != fact || await ReadAsync(source, fact.Name).ConfigureAwait(false) != fact)
                throw new InvalidDataException("Sequence state changed during migration: " + fact.Name);
        }
        await transaction.CommitAsync().ConfigureAwait(false);
        return new { sequences = facts.Count, allocationSize = SequenceIdSettings.AllocationSize, sourceDatabase = "sequence-id-test-db", targetDatabase = targetIdentity.Database, sourceUnchanged = true, newUnallocatedSequences = newNames };
    }

    static string SourceConnection()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_OFFLINE_SEQUENCE_SOURCE")
            ?? throw new InvalidOperationException("Set IFM_OFFLINE_SEQUENCE_SOURCE to the quiesced Development sequence database.");
        var identity = new NpgsqlConnectionStringBuilder(connection);
        if (identity.Host is not ("localhost" or "127.0.0.1") || !string.Equals(identity.Database, "sequence-id-test-db", StringComparison.Ordinal))
            throw new InvalidOperationException("Sequence source must be the named local Development sequence database.");
        return connection;
    }

    static async Task<HashSet<string>> ReadNamesAsync(NpgsqlConnection source)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var command = new NpgsqlCommand("SELECT sequencename FROM pg_sequences WHERE schemaname='public';", source);
        var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
            while (await reader.ReadAsync().ConfigureAwait(false)) names.Add(reader.GetString(0));
        return names;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "Sequence identifiers must exactly match checked-in SequenceName enum members; values never become arbitrary SQL identifiers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "PostgreSQL stores unquoted enum-derived sequence identifiers in lowercase.")]
    static async Task<SequenceState> ReadAsync(NpgsqlConnection connection, string name, NpgsqlTransaction? transaction = null)
    {
        if (!Enum.GetNames<SequenceName>().Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown sequence.", nameof(name));
        using var read = new NpgsqlCommand("SELECT last_value,is_called FROM public." + name + ";", connection, transaction);
        var reader = await read.ExecuteReaderAsync().ConfigureAwait(false);
        long last; bool called;
        await using (reader.ConfigureAwait(false))
        {
            if (!await reader.ReadAsync().ConfigureAwait(false)) throw new InvalidDataException("Missing sequence state.");
            last = reader.GetInt64(0); called = reader.GetBoolean(1);
        }
        using var increment = new NpgsqlCommand("SELECT increment_by FROM pg_sequences WHERE schemaname='public' AND sequencename=$1;", connection, transaction);
        increment.Parameters.AddWithValue(name.ToLowerInvariant());
        var size = Convert.ToInt64(await increment.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
        return new(name, last, called, size);
    }
    sealed record SequenceState(string Name, long Last, bool Called, long Increment);
}

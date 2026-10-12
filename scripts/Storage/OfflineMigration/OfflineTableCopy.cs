using System.Globalization;
using System.Text.RegularExpressions;
using Cassandra;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Storage.Tools;

/// <summary>Copies one quiesced Scylla table into an isolated native PostgreSQL table and verifies every mapped value.</summary>
internal static class OfflineTableCopy
{
    static readonly Regex Identifier = new("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    static readonly HashSet<string> SourceKeyspaces = new(StringComparer.Ordinal)
    { "market_data_test_db", "trade_test_db", "trade_plan_test_db", "reference_test_db", "securities_test_db", "option_pricer_test_db" };
    static readonly HashSet<string> TargetSchemas = new(StringComparer.Ordinal)
    { "market_data", "trade", "trade_plan", "reference", "securities", "option_pricer" };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "Schema is allowlisted and table/column identifiers are validated against a lowercase identifier grammar and database metadata.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "MA0051", Justification = "The offline copy, stable-source reread and target proof share one transaction; splitting ownership obscures its rollback boundary.")]
    public static async Task<object> RunAsync(string connection, string keyspace, string schema, string table)
    {
        var identity = new NpgsqlConnectionStringBuilder(connection);
        if (identity.Host is not ("localhost" or "127.0.0.1") ||
            !identity.Database.StartsWith("ifm_storage_migration_staging_", StringComparison.Ordinal))
            throw new InvalidOperationException("Offline copy is restricted to isolated local PostgreSQL staging databases.");
        if (!SourceKeyspaces.Contains(keyspace) || !TargetSchemas.Contains(schema) || !Identifier.IsMatch(table))
            throw new ArgumentException("Only inventoried Development source keyspaces, target schemas and simple table identifiers are accepted.", nameof(table));
        using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").WithPort(9042).Build();
        using var source = await cluster.ConnectAsync(keyspace).ConfigureAwait(false);
        var sourceColumns = await source.ExecuteAsync(new SimpleStatement("SELECT column_name FROM system_schema.columns WHERE keyspace_name=? AND table_name=?;", keyspace, table)).ConfigureAwait(false);
        var sourceNames = sourceColumns.Select(row => row.GetValue<string>("column_name")).ToHashSet(StringComparer.Ordinal);
        if (sourceNames.Count == 0) throw new InvalidDataException("Source table is missing.");
        var target = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connection);
        await using var targetDisposal = target.ConfigureAwait(false);
        await target.OpenAsync().ConfigureAwait(false);
        var columns = new List<Column>();
        using (var command = new NpgsqlCommand("SELECT column_name,udt_name,is_nullable,column_default FROM information_schema.columns WHERE table_schema=$1 AND table_name=$2 ORDER BY ordinal_position;", target))
        {
            command.Parameters.AddWithValue(schema); command.Parameters.AddWithValue(table);
            var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                columns.Add(new(reader.GetString(0), reader.GetString(1), string.Equals(reader.GetString(2), "YES", StringComparison.Ordinal), !await reader.IsDBNullAsync(3).ConfigureAwait(false)));
        }
        if (columns.Count == 0) throw new InvalidDataException("Create the reviewed target schema before copying.");
        if (columns.Any(column => !Identifier.IsMatch(column.Name))) throw new InvalidDataException("Target column identifier is not supported.");
        var targetNames = columns.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        if (sourceNames.Except(targetNames, StringComparer.Ordinal).Any())
            throw new InvalidDataException("Source columns are absent from the target; explicit lossless mapping is required.");
        if (columns.Any(x => !sourceNames.Contains(x.Name) && !x.Nullable && !x.HasDefault))
            throw new InvalidDataException("Target requires a field that the source does not contain.");
        var selected = columns.Where(x => sourceNames.Contains(x.Name)).ToArray();
        var names = string.Join(",", selected.Select(x => x.Name));
        var qualified = schema + "." + table;
        var statement = new SimpleStatement("SELECT " + names + " FROM " + keyspace + "." + table + ";")
            .SetPageSize(512).SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        var before = new RowFingerprint();
        var transaction = await target.BeginTransactionAsync().ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        using var lockCommand = new NpgsqlCommand("LOCK TABLE " + qualified + " IN ACCESS EXCLUSIVE MODE;", target, transaction);
        await lockCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        using var countCommand = new NpgsqlCommand("SELECT count(*) FROM " + qualified + ";", target, transaction);
        var existing = Convert.ToInt64(await countCommand.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (existing == 0)
        {
            var copy = await target.BeginBinaryImportAsync("COPY " + qualified + " (" + names + ") FROM STDIN (FORMAT BINARY)").ConfigureAwait(false);
            await using (copy.ConfigureAwait(false))
            {
                foreach (var row in await source.ExecuteAsync(statement).ConfigureAwait(false))
                {
                    var values = selected.Select(column => ConvertValue(row.GetValue<object>(column.Name), column.Type)).ToArray();
                    before.Add(values);
                    await copy.StartRowAsync().ConfigureAwait(false);
                    for (var index = 0; index < selected.Length; index++)
                    {
                        if (values[index] is null) await copy.WriteNullAsync().ConfigureAwait(false);
                        else await copy.WriteAsync(values[index]!, DbType(selected[index].Type)).ConfigureAwait(false);
                    }
                }
                await copy.CompleteAsync().ConfigureAwait(false);
            }
        }
        else
            foreach (var row in await source.ExecuteAsync(statement).ConfigureAwait(false))
                before.Add(selected.Select(column => ConvertValue(row.GetValue<object>(column.Name), column.Type)));
        var written = new RowFingerprint();
        using (var read = new NpgsqlCommand("SELECT " + names + " FROM " + qualified + ";", target, transaction))
        {
            var reader = await read.ExecuteReaderAsync().ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
                while (await reader.ReadAsync().ConfigureAwait(false))
                    written.Add(selected.Select((_, index) => reader.GetValue(index)));
        }
        var after = new RowFingerprint();
        foreach (var row in await source.ExecuteAsync(statement).ConfigureAwait(false))
            after.Add(selected.Select(column => ConvertValue(row.GetValue<object>(column.Name), column.Type)));
        if (before.Count != written.Count || !string.Equals(before.Digest, written.Digest, StringComparison.Ordinal) || before.Count != after.Count || !string.Equals(before.Digest, after.Digest, StringComparison.Ordinal))
            throw new InvalidDataException("Source changed or target keys/values differ; target table transaction is rolled back.");
        await transaction.CommitAsync().ConfigureAwait(false);
        return new { source = keyspace + "." + table, target = qualified, rows = before.Count, rowFingerprint = before.Digest,
            columns = selected.Select(x => x.Name), addedTargetColumns = columns.Where(x => !sourceNames.Contains(x.Name)).Select(x => x.Name),
            resumed = existing != 0, verifiedAtUtc = DateTime.UtcNow };
    }

    static object? ConvertValue(object? value, string type)
    {
        if (value is null) return null;
        return type switch
        {
            "date" when value is LocalDate date => DateOnly.Parse(date.ToString(), CultureInfo.InvariantCulture),
            "timestamptz" when value is DateTimeOffset time => time.UtcDateTime,
            "timestamptz" when value is DateTime time => time.Kind == DateTimeKind.Utc ? time : throw new InvalidDataException("Source timestamp is not UTC."),
            "time" when value is LocalTime time => time.TotalNanoseconds % 1000 == 0
                ? TimeSpan.FromTicks(time.TotalNanoseconds / 100)
                : throw new InvalidDataException("Source time contains sub-microsecond precision; an explicit lossless schema mapping is required."),
            "int2" => checked(Convert.ToInt16(value, CultureInfo.InvariantCulture)),
            "int4" => checked(Convert.ToInt32(value, CultureInfo.InvariantCulture)),
            "int8" => checked(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            "numeric" when value is decimal => value,
            "numeric" => throw new InvalidDataException("Numeric conversion requires a verified exact decimal source."),
            "_uuid" when value is IEnumerable<Guid> ids => ids.Order().ToArray(),
            "bytea" or "text" or "varchar" or "bool" or "uuid" or "float8" or "float4" => value,
            _ => throw new InvalidDataException("An explicit conversion is required for target type " + type)
        };
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S3265", Justification = "Npgsql documents combining its Array bit with the element type; its enum deliberately lacks Flags.")]
    static NpgsqlDbType DbType(string type) => type switch
    {
        "date" => NpgsqlDbType.Date, "time" => NpgsqlDbType.Time, "timestamptz" => NpgsqlDbType.TimestampTz,
        "int2" => NpgsqlDbType.Smallint, "int4" => NpgsqlDbType.Integer, "int8" => NpgsqlDbType.Bigint,
        "numeric" => NpgsqlDbType.Numeric, "bytea" => NpgsqlDbType.Bytea,
        "text" or "varchar" => NpgsqlDbType.Text, "bool" => NpgsqlDbType.Boolean,
        "uuid" => NpgsqlDbType.Uuid, "float8" => NpgsqlDbType.Double, "float4" => NpgsqlDbType.Real,
        "_uuid" => NpgsqlDbType.Array | NpgsqlDbType.Uuid,
        _ => throw new InvalidDataException("Unsupported PostgreSQL migration type " + type)
    };
    sealed record Column(string Name, string Type, bool Nullable, bool HasDefault);
}

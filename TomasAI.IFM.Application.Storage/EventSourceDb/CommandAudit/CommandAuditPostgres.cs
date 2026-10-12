using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;


namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

internal static class CommandAuditPostgres
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql documents Array and Range as modifiers that must be combined with the element type using bitwise OR; these values describe PostgreSQL parameter types.")]
    internal static async Task<CommandAuditWriteResult[]> ReserveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<CommandAuditEnvelope> envelopes,
        CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(
            EventSourceDbSql.InsertCommandLogMessagePackWindow, connection, transaction)
        { CommandTimeout = 3 };
        await using var commandDisposal = command.ConfigureAwait(false);
        Add(command, envelopes.Select(static item => item.CommandId).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        Add(command, envelopes.Select(static item => item.StreamId).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(command, envelopes.Select(static item => item.ActorName).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(command, envelopes.Select(static item => item.CommandName).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(command, envelopes.Select(static item => $"{item.CommandTimestampUtc:o}").ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(command, Enumerable.Repeat(CommandStatus.InProgress.ToString(), envelopes.Count).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(command, Enumerable.Repeat(string.Empty, envelopes.Count).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text);
        Add(command, envelopes.Select(static item => item.Payload.Bytes).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Bytea);
        Add(command, envelopes.Select(static item => (short)item.Payload.Format).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Smallint);
        Add(command, envelopes.Select(static item => item.Payload.Version).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Integer);
        Add(command, envelopes.Select(static item => item.Payload.Sha256).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Bytea);

        var results = new CommandAuditWriteResult[envelopes.Count];
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var count = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var ordinal = checked((int)reader.GetInt64(0) - 1);
            if ((uint)ordinal >= (uint)results.Length) throw new InvalidDataException("Invalid command audit ordinal.");
            var accepted = reader.GetBoolean(2);
            var payloadConflict = false;
            if (!accepted && !(await reader.IsDBNullAsync(3,cancellationToken).ConfigureAwait(false)))
            {
                var storedHash = (await reader.GetFieldValueAsync<byte[]>(3,cancellationToken).ConfigureAwait(false));
                if (storedHash.Length != SHA256.HashSizeInBytes ||
                    !CryptographicOperations.FixedTimeEquals(storedHash, envelopes[ordinal].Payload.Sha256))
                    payloadConflict = true;
            }
            results[ordinal] = new CommandAuditWriteResult(
                accepted,
                !accepted && (await reader.IsDBNullAsync(3,cancellationToken).ConfigureAwait(false)),
                payloadConflict);
            count++;
        }
        if (count != envelopes.Count) throw new InvalidDataException("PostgreSQL returned an incomplete command audit result.");
        await reader.DisposeAsync().ConfigureAwait(false);
        // Cold-cache/restart compatibility: existing audit hashes remain hashes of
        // the complete original bytes. Compare normalized identity only on a retry.
        // A new statement also sees a competing INSERT committed after the CTE snapshot.
        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].Accepted || envelopes[index].RetryCommandType is null) continue;
            var matches = await MatchesStoredRetryAsync(connection, transaction, envelopes[index], cancellationToken).ConfigureAwait(false);
            results[index] = new(false, false, !matches);
        }
        return results;
    }

    static async Task<bool> MatchesStoredRetryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CommandAuditEnvelope candidate, CancellationToken token)
    {
        var read = new NpgsqlCommand("SELECT commandpayload,commandpayloadformat,commandpayloadversion,commandpayloadsha256 FROM command_log WHERE commandid=$1", connection, transaction) { CommandTimeout = 3 };
        await using var readDisposal = read.ConfigureAwait(false);
        read.Parameters.AddWithValue(candidate.CommandId);
        var reader = await read.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false) || Enumerable.Range(0, 4).Any(reader.IsDBNull)) return false;
        var bytes = (await reader.GetFieldValueAsync<byte[]>(0,token).ConfigureAwait(false));
        var hash = (await reader.GetFieldValueAsync<byte[]>(3,token).ConfigureAwait(false));
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), hash)) return false;
        var codec = new CommandAuditMessagePackCodec();
        var stored = codec.Deserialize(candidate.RetryCommandType!, bytes, reader.GetInt16(1), reader.GetInt32(2));
        // Fail closed if decoding under the candidate type would drop unknown fields
        // or otherwise reinterpret the original audit envelope.
        return stored is TomasAI.IFM.Shared.EventSourcing.ICommandRetryIdentity retry
            && codec.Serialize(retry).Bytes.AsSpan().SequenceEqual(bytes)
            && CryptographicOperations.FixedTimeEquals(codec.Serialize(retry.ForRetryIdentity()).Sha256, candidate.RetrySha256);
    }

    static void Add(NpgsqlCommand command, object value, NpgsqlDbType type)
        => command.Parameters.Add(new NpgsqlParameter { Value = value, NpgsqlDbType = type });
}

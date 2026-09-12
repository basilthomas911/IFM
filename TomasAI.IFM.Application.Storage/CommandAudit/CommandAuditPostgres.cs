using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;

namespace TomasAI.IFM.Application.Storage.CommandAudit;

internal static class CommandAuditPostgres
{
    internal static async Task<CommandAuditWriteResult[]> ReserveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<CommandAuditEnvelope> envelopes,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            EventSourceDbSql.InsertCommandLogMessagePackWindow, connection, transaction) { CommandTimeout = 3 };
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
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var count = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var ordinal = checked((int)reader.GetInt64(0) - 1);
            if ((uint)ordinal >= (uint)results.Length) throw new InvalidDataException("Invalid command audit ordinal.");
            var accepted = reader.GetBoolean(2);
            if (!accepted && !reader.IsDBNull(3))
            {
                var storedHash = reader.GetFieldValue<byte[]>(3);
                if (storedHash.Length != SHA256.HashSizeInBytes ||
                    !CryptographicOperations.FixedTimeEquals(storedHash, envelopes[ordinal].Payload.Sha256))
                    throw new CommandAuditPayloadConflictException(envelopes[ordinal].CommandId);
            }
            results[ordinal] = new CommandAuditWriteResult(accepted, !accepted && reader.IsDBNull(3));
            count++;
        }
        if (count != envelopes.Count) throw new InvalidDataException("PostgreSQL returned an incomplete command audit result.");
        return results;
    }

    static void Add(NpgsqlCommand command, object value, NpgsqlDbType type)
        => command.Parameters.Add(new NpgsqlParameter { Value = value, NpgsqlDbType = type });
}

using System.Buffers;
using System.Data;
using MessagePack;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public sealed partial class TradeDbContext
{
    /// <inheritdoc/>
    public Task<IntrinsicTimeStrategyWorkflowView?> GetIntrinsicTimeStrategyWorkflowViewAsync(
        StrategyWorkflowId workflowId, CancellationToken cancellationToken) =>
        ReadWorkflowViewAsync(workflowId, forGrid: false, cancellationToken);

    /// <inheritdoc/>
    public async Task<IntrinsicTimeStrategyUpdatesGridRow?> GetIntrinsicTimeStrategyUpdatesGridRowAsync(
        StrategyWorkflowId workflowId, CancellationToken cancellationToken)
    {
        var view = await ReadWorkflowViewAsync(workflowId, forGrid: true, cancellationToken).ConfigureAwait(false);
        return view is null ? null : IntrinsicTimeStrategyUpdatesGridRow.FromView(view);
    }

    async Task<IntrinsicTimeStrategyWorkflowView?> ReadWorkflowViewAsync(
        StrategyWorkflowId workflowId, bool forGrid, CancellationToken cancellationToken)
    {
        byte[]? buffer = null;
        int length = 0;
        string entityId;
        long revision;
        try
        {
            // Release the reader and any owned connection before decoding. The buffer remains leased
            // until MessagePack has created the fully owned workflow view.
            {
                var ambientCommand = InTransaction() as NpgsqlCommand;
                var connection = ambientCommand is null ? CreateConnection().As<NpgsqlConnection>(ConnectionString) : null;
                await using var ownedConnection = connection;
                if (connection is not null)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var command = ambientCommand ?? connection!.CreateCommand();
                await using var commandScope = command.ConfigureAwait(false);
                command.CommandText = TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowView;
                command.Parameters.Add(new NpgsqlParameter<Guid> { NpgsqlDbType = NpgsqlDbType.Uuid, TypedValue = workflowId.Value });

                var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow, cancellationToken)
                    .ConfigureAwait(false);
                await using var readerScope = reader.ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    return null;
                entityId = reader.GetString(0);
                revision = reader.GetInt64(1);
                var schema = reader.GetInt32(2);
                if (schema is < 1 or > 2)
                    throw new NotSupportedException($"Workflow projection schema {schema} requires migration.");
                length = reader.GetInt32(3);
                if (length <= 0)
                    throw new InvalidDataException("Workflow projection payload is empty.");

                buffer = ArrayPool<byte>.Shared.Rent(length);
                var stream = await reader.GetStreamAsync(4, cancellationToken).ConfigureAwait(false);
                await using var streamScope = stream.ConfigureAwait(false);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            }

            var payload = buffer.AsMemory(0, length);
            var view = forGrid
                ? StrategyStageResultEnvelope.DecodeForGrid(payload, cancellationToken)
                : MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(
                    payload, MessagePackBinarySerializer.Options, cancellationToken);
            if (view is null || view.WorkflowId != workflowId || view.WorkflowRevision != revision
                || !string.Equals(view.EntityId.Format(), entityId, StringComparison.Ordinal))
                throw new InvalidDataException("Workflow projection payload does not match its stored identity and revision.");
            return view;
        }
        finally
        {
            if (buffer is not null)
            {
                buffer.AsSpan(0, length).Clear();
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}

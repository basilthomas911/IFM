using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial class TradeDbContext
{
    /// <inheritdoc />
    public Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(StrategyWorkflowId workflowId)
        => GetRegimeDiscoveryAsync(workflowId, CancellationToken.None);

    /// <inheritdoc />
    public Task<RegimeDiscoveryReadModel?> GetRegimeDiscoveryAsync(
        StrategyWorkflowId workflowId,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetRegimeDiscovery)}", TradeDbCql.GetRegimeDiscovery)
            .SetParameters(new GetRegimeDiscovery(workflowId.Value))
            .ExecuteSingleAsync(MapRegimeDiscovery, cancellationToken);

    /// <inheritdoc />
    public Task UpsertRegimeDiscoveryAsync(RegimeDiscoveryReadModel result)
        => UpsertRegimeDiscoveryAsync(result, CancellationToken.None);

    /// <inheritdoc />
    public async Task UpsertRegimeDiscoveryAsync(
        RegimeDiscoveryReadModel result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertRegimeDiscovery)}", TradeDbCql.UpsertRegimeDiscovery)
            .SetParameters(new UpsertRegimeDiscovery(
                result.WorkflowId.Value,
                result.WorkflowEntityId,
                result.InputWorkflowRevision,
                result.CommandId,
                result.SourceEventId,
                result.SourceEventSequence,
                result.Status,
                result.ParameterPayloadSha256,
                result.SignalSnapshotId,
                result.ResultPayload.ToArray(),
                result.ResultPayloadSha256,
                result.FailureCode,
                result.FailureMessage,
                result.ReasonsPayload.ToArray(),
                result.SchemaVersion,
                result.TerminalAtUtc,
                result.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    static RegimeDiscoveryReadModel MapRegimeDiscovery(IObjectDataRecord row) => new()
    {
        WorkflowId = new StrategyWorkflowId(row.GetGuid(0)),
        WorkflowEntityId = row.GetString(1),
        InputWorkflowRevision = row.GetLong(2),
        CommandId = row.GetGuid(3),
        SourceEventId = row.GetGuid(4),
        SourceEventSequence = row.GetLong(5),
        Status = row.GetString(6),
        ParameterPayloadSha256 = row.GetString(7),
        SignalSnapshotId = row.GetGuid(8),
        ResultPayload = row.IsNull(9) ? ReadOnlyMemory<byte>.Empty : row.GetBytes(9),
        ResultPayloadSha256 = row.IsNull(10) ? string.Empty : row.GetString(10),
        FailureCode = row.IsNull(11) ? 0 : row.GetInt(11),
        FailureMessage = row.IsNull(12) ? string.Empty : row.GetString(12),
        ReasonsPayload = row.IsNull(13) ? ReadOnlyMemory<byte>.Empty : row.GetBytes(13),
        SchemaVersion = row.GetInt(14),
        TerminalAtUtc = row.GetDateTime(15),
        UpdatedAtUtc = row.GetDateTime(16)
    };
}

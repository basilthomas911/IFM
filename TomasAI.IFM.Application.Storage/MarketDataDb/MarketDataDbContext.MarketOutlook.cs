using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    /// <summary>Projects an immutable Market Outlook accumulation checkpoint.</summary>
    public async Task UpsertMarketOutlookWorkingStateAsync(
        MarketOutlookWorkingStateReadModel workingState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingState);
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpsertMarketOutlookWorkingState)}", MarketDataDbCql.UpsertMarketOutlookWorkingState)
            .SetParameters(new UpsertMarketOutlookWorkingState(
                workingState.EntityId.ContractId,
                workingState.EntityId.ValueDate,
                workingState.Revision,
                workingState.UpdatedOn,
                workingState.Status.ToString(),
                MessagePackSerializer.Serialize(workingState)))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets one projected Market Outlook accumulation checkpoint.</summary>
    public async Task<MarketOutlookWorkingStateReadModel?> GetMarketOutlookWorkingStateAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
        => await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketOutlookWorkingState)}", MarketDataDbCql.GetMarketOutlookWorkingState)
            .SetParameters(new GetMarketOutlookWorkingState(contractId, valueDate))
            .ExecuteSingleAsync(
                static row => MessagePackSerializer.Deserialize<MarketOutlookWorkingStateReadModel>(
                    row.GetBytes(0)),
                cancellationToken)
            .ConfigureAwait(false);

    public async Task UpsertMarketOutlookSnapshotAsync(
        MarketOutlookSnapshotReadModel snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var tradeSignal = snapshot.FuturesTradeSignal is null
            ? null
            : MessagePackSerializer.Serialize(snapshot.FuturesTradeSignal);
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpsertMarketOutlookSnapshot)}", MarketDataDbCql.UpsertMarketOutlookSnapshot)
            .SetParameters(new UpsertMarketOutlookSnapshot(
                snapshot.ContractId,
                snapshot.ValueDate,
                snapshot.Revision,
                snapshot.UpdatedOn,
                MessagePackSerializer.Serialize(snapshot.FuturesEodData),
                tradeSignal,
                snapshot.MissingInputs))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MarketOutlookSnapshotReadModel?> GetMarketOutlookSnapshotAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        // The working-state blob carries the independently admitted component snapshots. Prefer it
        // so a query returns the same OR-composite contract emitted by realtime notification. The
        // legacy snapshot columns remain as a backward-compatible fallback during rollout.
        var workingState = await GetMarketOutlookWorkingStateAsync(
            contractId,
            valueDate,
            cancellationToken).ConfigureAwait(false);
        if (workingState?.PublishedSnapshot is { IsValid: true } published)
            return published;

        return await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketOutlookSnapshot)}", MarketDataDbCql.GetMarketOutlookSnapshot)
            .SetParameters(new GetMarketOutlookSnapshot(contractId, valueDate))
            .ExecuteSingleAsync(MapToMarketOutlookSnapshot, cancellationToken)
            .ConfigureAwait(false);
    }

    static MarketOutlookSnapshotReadModel MapToMarketOutlookSnapshot<TDataRecord>(
        TDataRecord row)
        where TDataRecord : IObjectDataRecord
        => new(
            row.GetString(0),
            row.GetDateOnly(1),
            row.GetLong(2),
            row.GetDateTime(3),
            MessagePackSerializer.Deserialize<FuturesEodDataV2ReadModel>(row.GetBytes(4)),
            row.IsNull(5)
                ? null
                : MessagePackSerializer.Deserialize<FuturesTradeSignalV2ReadModel>(row.GetBytes(5)),
            row.IsNull(6) ? string.Empty : row.GetString(6));
}

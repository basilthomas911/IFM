using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    static readonly MessagePackSerializerOptions MarketOutlookSerializerOptions =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

    public async Task UpsertMarketOutlookSnapshotAsync(
        MarketOutlookReadModel snapshot,
        long revision = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var payload = MessagePackSerializer.Serialize(snapshot, MarketOutlookSerializerOptions);
        var eod = MessagePackSerializer.Serialize(snapshot.FuturesEodData, MarketOutlookSerializerOptions);
        var tradeSignal = snapshot.FuturesTradeSignal is null
            ? null
            : MessagePackSerializer.Serialize(snapshot.FuturesTradeSignal, MarketOutlookSerializerOptions);
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.UpsertMarketOutlookSnapshot)}",
                MarketDataDbCql.UpsertMarketOutlookSnapshot)
            .SetParameters(new UpsertMarketOutlookSnapshot(
                snapshot.ContractId,
                snapshot.ValueDate,
                revision,
                snapshot.UpdatedAtUtc,
                eod,
                tradeSignal,
                snapshot.MissingInputs,
                payload))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MarketOutlookReadModel?> GetMarketOutlookSnapshotAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contractId) || valueDate == default)
            return null;
        return await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketOutlookSnapshot)}",
                MarketDataDbCql.GetMarketOutlookSnapshot)
            .SetParameters(new GetMarketOutlookSnapshot(contractId, valueDate))
            .ExecuteSingleAsync(MapMarketOutlookSnapshot, cancellationToken)
            .ConfigureAwait(false);
    }

    static MarketOutlookReadModel MapMarketOutlookSnapshot(IObjectDataRecord row)
    {
        var contractId = row.GetString(1);
        var valueDate = row.GetDateOnly(2);
        var payload = row.GetBytes(0);
        var snapshot = payload.Length == 0
            ? MapLegacyMarketOutlookSnapshot(row, contractId, valueDate)
            : MessagePackSerializer.Deserialize<MarketOutlookReadModel>(
                payload, MarketOutlookSerializerOptions);
        if (!string.Equals(snapshot.ContractId, contractId, StringComparison.Ordinal)
            || snapshot.ValueDate != valueDate)
            throw new InvalidDataException(
                $"Market Outlook snapshot payload identity '{snapshot.ContractId}.{snapshot.ValueDate:yyyyMMdd}' " +
                $"does not match row identity '{contractId}.{valueDate:yyyyMMdd}'.");
        return snapshot;
    }

    static MarketOutlookReadModel MapLegacyMarketOutlookSnapshot(
        IObjectDataRecord row,
        string contractId,
        DateOnly valueDate)
    {
        var eodPayload = row.GetBytes(5);
        if (eodPayload.Length == 0)
            throw new InvalidDataException(
                $"Market Outlook row '{contractId}.{valueDate:yyyyMMdd}' has neither a snapshot nor legacy EOD data.");

        var updatedAtUtc = row.GetDateTime(4);
        var tradeSignalPayload = row.GetBytes(6);
        var eod = MessagePackSerializer.Deserialize<FuturesEodDataV2ReadModel>(
            eodPayload, MarketOutlookSerializerOptions);
        var tradeSignal = tradeSignalPayload.Length == 0
            ? null
            : MessagePackSerializer.Deserialize<FuturesTradeSignalV2ReadModel>(
                tradeSignalPayload, MarketOutlookSerializerOptions);

        return new MarketOutlookReadModel
        {
            ContractId = contractId,
            ValueDate = valueDate,
            UpdatedAtUtc = updatedAtUtc,
            MarketDataAsOfUtc = updatedAtUtc,
            RefreshTrigger = MarketOutlookRefreshTrigger.PersistedBaseline,
            FuturesEodData = eod,
            FuturesTradeSignal = tradeSignal,
            MissingInputs = row.IsNull(7) ? string.Empty : row.GetString(7),
            EsPriceAvailability = eod.IsValid
                ? MarketOutlookInputAvailability.Available
                : MarketOutlookInputAvailability.Unavailable
        };
    }
}

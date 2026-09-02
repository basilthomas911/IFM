using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
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
        var snapshot = MessagePackSerializer.Deserialize<MarketOutlookReadModel>(
            row.GetBytes(0), MarketOutlookSerializerOptions);
        var contractId = row.GetString(1);
        var valueDate = row.GetDateOnly(2);
        if (!string.Equals(snapshot.ContractId, contractId, StringComparison.Ordinal)
            || snapshot.ValueDate != valueDate)
            throw new InvalidDataException(
                $"Market Outlook snapshot payload identity '{snapshot.ContractId}.{snapshot.ValueDate:yyyyMMdd}' " +
                $"does not match row identity '{contractId}.{valueDate:yyyyMMdd}'.");
        return snapshot;
    }
}

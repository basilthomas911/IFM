using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    /// <inheritdoc />
    public Task InsertFuturesVwapSignalAsync(
        FuturesVwapSignalReadModel signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesVwapSignal)}",
                MarketDataDbCql.InsertFuturesVwapSignal)
            .SetParameters(new InsertFuturesVwapSignal(
                signal.ContractId, signal.ValueDate, signal.ConfigurationId,
                signal.AsOfUtc.UtcDateTime, signal.LastTradeOrdinal,
                signal.SessionStartUtc.UtcDateTime, signal.SessionEndUtc.UtcDateTime,
                signal.CumulativePriceVolume, signal.CumulativeVolume,
                signal.EligibleTradeCount, signal.RejectedTradeCount, signal.LastPrice,
                signal.Vwap, signal.PriceMinusVwap, signal.PriceToVwapPercent,
                signal.LastTradeSourceSequence, signal.StreamEpochId,
                signal.IsWarm, signal.IsValid, signal.InvalidReason.ToString(),
                signal.IsTickExact, signal.CalculationMethod.ToString(),
                signal.SchemaVersion, signal.CalculationVersion))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<FuturesVwapSignalReadModel?> GetLatestFuturesVwapSignalAsync(
        string contractId, DateOnly valueDate, string configurationId,
        CancellationToken cancellationToken = default) =>
        _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLatestFuturesVwapSignal)}",
                MarketDataDbCql.GetLatestFuturesVwapSignal)
            .SetParameters(new GetLatestFuturesVwapSignal(contractId, valueDate, configurationId))
            .ExecuteSingleAsync(MapToFuturesVwapSignal!, cancellationToken);

    /// <inheritdoc />
    public async Task<ICollection<FuturesVwapSignalReadModel>> GetFuturesVwapSignalHistoryAsync(
        string contractId, DateOnly valueDate, string configurationId,
        CancellationToken cancellationToken = default) =>
        await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesVwapSignalHistory)}",
                MarketDataDbCql.GetFuturesVwapSignalHistory)
            .SetParameters(new GetFuturesVwapSignalHistory(contractId, valueDate, configurationId))
            .ExecuteQueryAsync(MapToFuturesVwapSignal!, cancellationToken);

    static FuturesVwapSignalReadModel MapToFuturesVwapSignal<TDataRecord>(
        TDataRecord row) where TDataRecord : IObjectDataRecord => new()
    {
        ContractId = row.GetString(0),
        ValueDate = row.GetDateOnly(1),
        ConfigurationId = row.GetString(2),
        SessionStartUtc = new(row.GetDateTime(3), TimeSpan.Zero),
        SessionEndUtc = new(row.GetDateTime(4), TimeSpan.Zero),
        AsOfUtc = new(row.GetDateTime(5), TimeSpan.Zero),
        CumulativePriceVolume = row.GetDecimal(6),
        CumulativeVolume = row.GetLong(7),
        EligibleTradeCount = row.GetLong(8),
        RejectedTradeCount = row.GetLong(9),
        LastPrice = row.GetDecimal(10),
        Vwap = row.IsNull(11) ? null : row.GetDecimal(11),
        PriceMinusVwap = row.IsNull(12) ? null : row.GetDecimal(12),
        PriceToVwapPercent = row.IsNull(13) ? null : row.GetDecimal(13),
        LastTradeSourceSequence = row.GetLong(14),
        StreamEpochId = row.GetGuid(15),
        LastTradeOrdinal = row.GetLong(16),
        IsWarm = row.GetBool(17),
        IsValid = row.GetBool(18),
        InvalidReason = Enum.Parse<FuturesVwapInvalidReason>(row.GetString(19)),
        IsTickExact = row.GetBool(20),
        CalculationMethod = Enum.Parse<FuturesVwapCalculationMethod>(row.GetString(21)),
        SchemaVersion = row.GetInt(22),
        CalculationVersion = row.GetString(23)
    };
}

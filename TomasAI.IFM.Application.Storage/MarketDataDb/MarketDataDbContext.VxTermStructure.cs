using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    /// <inheritdoc />
    public Task InsertFuturesVxTermStructureSignalAsync(
        FuturesVxTermStructureSignalReadModel signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertFuturesVxTermStructureSignal)}",
                MarketDataDbCql.InsertFuturesVxTermStructureSignal)
            .SetParameters(new InsertFuturesVxTermStructureSignal(
                signal.ValueDate, signal.ConfigurationId, signal.CalculatedAtUtc.UtcDateTime,
                signal.FrontSourceSequence, signal.BackSourceSequence,
                signal.FrontVxContractId, signal.FrontExpiry, signal.FrontVxPrice,
                signal.BackVxContractId, signal.BackExpiry, signal.BackVxPrice,
                signal.FrontBackSpread, signal.FrontBackRatio, signal.TermStructurePercent,
                signal.TermStructureState.ToString(), signal.PriorFrontBackRatio,
                signal.PriorTermStructurePercent, signal.FrontSourceTimestampUtc.UtcDateTime,
                signal.BackSourceTimestampUtc.UtcDateTime, signal.IsWarm, signal.IsValid,
                signal.SchemaVersion, signal.CalculationVersion))
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<FuturesVxTermStructureSignalReadModel?> GetLatestFuturesVxTermStructureSignalAsync(
        DateOnly valueDate,
        string configurationId,
        CancellationToken cancellationToken = default) =>
        _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLatestFuturesVxTermStructureSignal)}",
                MarketDataDbCql.GetLatestFuturesVxTermStructureSignal)
            .SetParameters(new GetLatestFuturesVxTermStructureSignal(valueDate, configurationId))
            .ExecuteSingleAsync(MapToFuturesVxTermStructureSignal!, cancellationToken);

    static FuturesVxTermStructureSignalReadModel MapToFuturesVxTermStructureSignal<TDataRecord>(
        TDataRecord row) where TDataRecord : IObjectDataRecord => new()
    {
        ValueDate = row.GetDateOnly(0),
        ConfigurationId = row.GetString(1),
        FrontVxContractId = row.GetString(2),
        FrontExpiry = row.GetDateOnly(3),
        FrontVxPrice = row.GetDecimal(4),
        BackVxContractId = row.GetString(5),
        BackExpiry = row.GetDateOnly(6),
        BackVxPrice = row.GetDecimal(7),
        FrontBackSpread = row.GetDecimal(8),
        FrontBackRatio = row.GetDecimal(9),
        TermStructurePercent = row.GetDecimal(10),
        TermStructureState = Enum.Parse<FuturesVxTermStructureState>(row.GetString(11)),
        PriorFrontBackRatio = row.IsNull(12) ? null : row.GetDecimal(12),
        PriorTermStructurePercent = row.IsNull(13) ? null : row.GetDecimal(13),
        FrontSourceTimestampUtc = new(row.GetDateTime(14), TimeSpan.Zero),
        BackSourceTimestampUtc = new(row.GetDateTime(15), TimeSpan.Zero),
        FrontSourceSequence = row.GetLong(16),
        BackSourceSequence = row.GetLong(17),
        CalculatedAtUtc = new(row.GetDateTime(18), TimeSpan.Zero),
        IsWarm = row.GetBool(19),
        IsValid = row.GetBool(20),
        SchemaVersion = row.GetInt(21),
        CalculationVersion = row.GetString(22)
    };
}

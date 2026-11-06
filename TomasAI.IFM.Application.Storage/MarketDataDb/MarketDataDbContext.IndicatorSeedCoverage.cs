using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FuturesTradeSessionBarReadModel>> GetQualifiedIndicatorSeedBarsAsync(string contractId,
        TimeFrameType frame, DateOnly valueDate, DateTimeOffset[] intervalEndsUtc, string sourceVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVersion);
        ArgumentNullException.ThrowIfNull(intervalEndsUtc);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(intervalEndsUtc.Length, 64);
        if (intervalEndsUtc.Length == 0) return [];

        return (await Database.Use(nameof(MarketDataDbSql.GetQualifiedIndicatorSeedBars), MarketDataDbSql.GetQualifiedIndicatorSeedBars)
            .SetParameters(new IndicatorSeedCoverageKey(contractId, frame, valueDate, intervalEndsUtc, sourceVersion))
            .ExecuteQueryAsync(row => row.IsNull(17) ? MapStoredTradeSessionBar(row, contractId, frame, valueDate)
                : MapCachedIndicatorSeedBar(row, contractId, frame, valueDate), cancellationToken)
            .ConfigureAwait(false)).ToArray();
    }

    /// <inheritdoc />
    public async Task StoreQualifiedIndicatorSeedBarsAsync(string sourceVersion, FuturesTradeSessionBarReadModel[] bars,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceVersion);
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bars.Length, 64);
        if (bars.Length == 0) return;

        var first = bars[0];
        var ends = new HashSet<DateTimeOffset>();
        foreach (var bar in bars)
        {
            if (!bar.IsComplete || !bar.IsValid || bar.ContractId != first.ContractId
                || bar.TimeFrame != first.TimeFrame || bar.ValueDate != first.ValueDate
                || bar.IntervalStartUtc >= bar.IntervalEndUtc || !ends.Add(bar.IntervalEndUtc))
                throw new ArgumentException("Seed coverage must contain unique qualified intervals of the same contract, timeframe and ValueDate.", nameof(bars));
        }

        await Database.Use(nameof(MarketDataDbSql.StoreQualifiedIndicatorSeedBars), MarketDataDbSql.StoreQualifiedIndicatorSeedBars)
            .SetParameters(new IndicatorSeedCoverageBatch(sourceVersion, bars))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    static FuturesTradeSessionBarReadModel MapCachedIndicatorSeedBar(IObjectDataRecord row, string contractId,
        TimeFrameType frame, DateOnly valueDate)
    {
        var bar = MessagePackBinarySerializer.Shared.Deserialize<FuturesTradeSessionBarReadModel>(row.GetBytes(17));
        var end = new DateTimeOffset(DateTime.SpecifyKind(row.GetDateTime(2), DateTimeKind.Utc));
        if (bar is null || bar.ContractId != contractId || bar.TimeFrame != frame || bar.ValueDate != valueDate
            || bar.IntervalEndUtc != end || bar is not { IsComplete: true, IsValid: true })
            throw new InvalidDataException("Stored indicator seed payload does not match its qualified interval identity.");
        return bar;
    }

    readonly record struct IndicatorSeedCoverageKey(string ContractId, TimeFrameType Frame, DateOnly Date,
        DateTimeOffset[] Ends, string SourceVersion) : IBindValue
    {
        public object Bind() => new object?[] { MarketSeriesIdentity.ForContract(ContractId).Format(), Frame.ToString(),
            checked(Date.Year * 100 + Date.Month), Ends.Select(end => end.UtcDateTime).ToArray(), ContractId, Date, SourceVersion };
    }

    readonly record struct IndicatorSeedCoverageBatch(string SourceVersion, FuturesTradeSessionBarReadModel[] Bars) : IBindValue
    {
        public object Bind() => new object?[] { Bars[0].ContractId, Bars[0].TimeFrame.ToString(), Bars[0].ValueDate, SourceVersion,
            Bars.Select(bar => bar.IntervalEndUtc.UtcDateTime).ToArray(),
            Bars.Select(bar => MessagePackBinarySerializer.Shared.Serialize(bar)!).ToArray() };
    }
}

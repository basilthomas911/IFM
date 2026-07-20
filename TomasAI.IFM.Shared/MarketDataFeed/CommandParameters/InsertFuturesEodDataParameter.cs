using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to insert futures end-of-day data.
/// </summary>
/// <param name="ValueDate">The value (trading) date.</param>
/// <param name="FuturesTickData">The futures tick data snapshot.</param>
/// <param name="Contract">The futures contract metadata.</param>
/// <param name="EodDataToday">The current end-of-day data.</param>
/// <param name="EodDataRange">Historical end-of-day data range.</param>
/// <param name="NormCurveData">Normal curve table data.</param>
/// <param name="WindowSize">The calculation window size.</param>
/// <param name="VixEodData">VIX futures end-of-day data.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertFuturesEodDataParameter(
    DateOnly ValueDate,
    FuturesTickDataV2ReadModel FuturesTickData,
    FuturesContractV2ReadModel Contract,
    FuturesEodDataV2ReadModel EodDataToday,
    ICollection<FuturesEodDataV2ReadModel> EodDataRange,
    NormalCurveTableReadModel NormCurveData,
    int WindowSize,
    ICollection<VixFuturesEodDataReadModel> VixEodData,
    int ErrorCode)
    : ICommandParameter;

using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>Checks initialized five-minute indicators independently of price-feed and daily-analytics readiness.</summary>
public static class SessionIndicatorReadiness
{
    /// <summary>Requires warm ADX, ATR, MACD and RSI plus computed TDI for the requested contract and session.</summary>
    public static bool IsReady(MarketOutlookInputState inputs, string contractId, DateOnly valueDate)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return inputs.EntityId.ContractId == contractId && inputs.EntityId.ValueDate == valueDate
            && inputs.FuturesAdxSignal is { IsWarm: true, TimePeriod: TimeFrameType.FiveMinutes } adx
            && adx.ValueDate == valueDate && adx.ContractId == contractId
            && inputs.FuturesAtrSignal is { IsWarm: true, TimePeriod: TimeFrameType.FiveMinutes } atr
            && atr.ValueDate == valueDate && atr.ContractId == contractId
            && inputs.FuturesMacdSignal is { IsWarm: true, TimePeriod: TimeFrameType.FiveMinutes } macd
            && macd.ValueDate == valueDate && macd.ContractId == contractId
            && inputs.FuturesRsiSignal is { IsWarm: true, TimePeriod: TimeFrameType.FiveMinutes } rsi
            && rsi.ValueDate == valueDate && rsi.ContractId == contractId
            && inputs.FuturesTdiSignal is { SchemaVersion: >= 2, TimePeriod: TimeFrameType.FiveMinutes } tdi
            && tdi.ValueDate == valueDate && tdi.ContractId == contractId;
    }
}

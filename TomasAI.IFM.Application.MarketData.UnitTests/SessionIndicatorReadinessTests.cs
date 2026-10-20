using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class SessionIndicatorReadinessTests
{
    [Theory]
    [InlineData("ready", true)]
    [InlineData("adx", false)]
    [InlineData("atr", false)]
    [InlineData("macd", false)]
    [InlineData("rsi", false)]
    [InlineData("tdi", false)]
    [InlineData("previous-session", false)]
    [InlineData("wrong-timeframe", false)]
    public void Only_current_session_warm_five_minute_indicators_are_ready(string scenario, bool expected)
    {
        var date = new DateOnly(2026, 10, 20);
        var inputs = new MarketOutlookInputState {
            EntityId = new("ES", date),
            FuturesAdxSignal = new() { ContractId = "ES", ValueDate = date, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
            FuturesAtrSignal = new() { ContractId = "ES", ValueDate = date, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
            FuturesMacdSignal = new() { ContractId = "ES", ValueDate = date, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
            FuturesRsiSignal = new() { ContractId = "ES", ValueDate = date, TimePeriod = TimeFrameType.FiveMinutes, IsWarm = true },
            FuturesTdiSignal = new() { ContractId = "ES", ValueDate = date, TimePeriod = TimeFrameType.FiveMinutes, SchemaVersion = 2 }
        };
        inputs = scenario switch {
            "adx" => inputs with { FuturesAdxSignal = inputs.FuturesAdxSignal with { IsWarm = false } },
            "atr" => inputs with { FuturesAtrSignal = inputs.FuturesAtrSignal with { IsWarm = false } },
            "macd" => inputs with { FuturesMacdSignal = inputs.FuturesMacdSignal with { IsWarm = false } },
            "rsi" => inputs with { FuturesRsiSignal = inputs.FuturesRsiSignal with { IsWarm = false } },
            "tdi" => inputs with { FuturesTdiSignal = null },
            "previous-session" => inputs with { FuturesRsiSignal = inputs.FuturesRsiSignal with { ValueDate = date.AddDays(-1) } },
            "wrong-timeframe" => inputs with { FuturesRsiSignal = inputs.FuturesRsiSignal with { TimePeriod = TimeFrameType.FifteenSeconds } },
            _ => inputs
        };
        Assert.Equal(expected, SessionIndicatorReadiness.IsReady(inputs, "ES", date));
    }
}

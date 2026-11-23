using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class FiveMinuteEmaDirectionTests
{
    [Theory]
    [InlineData(30, 20, 10, true, true, TimeFrameType.FiveMinutes, 1, "StronglyBullish")]
    [InlineData(10, 20, 30, true, true, TimeFrameType.FiveMinutes, 1, "StronglyBearish")]
    [InlineData(20, 30, 10, true, true, TimeFrameType.FiveMinutes, 1, "RangeBound")]
    [InlineData(20, 20, 10, true, true, TimeFrameType.FiveMinutes, 1, "RangeBound")]
    [InlineData(30, 20, 10, false, true, TimeFrameType.FiveMinutes, 1, "Unavailable")]
    [InlineData(30, 20, 10, true, false, TimeFrameType.FiveMinutes, 1, "Unavailable")]
    [InlineData(30, 20, 10, true, true, TimeFrameType.Daily, 1, "Unavailable")]
    [InlineData(30, 20, 10, true, true, TimeFrameType.FiveMinutes, 20, "Unavailable")]
    [InlineData(30, 20, 0, true, true, TimeFrameType.FiveMinutes, 1, "Unavailable")]
    public void Outlook_direction_uses_only_qualified_five_minute_stack(int fast, int medium, int slow,
        bool warm, bool valid, TimeFrameType frame, int ageMinutes, string expected)
    {
        var now = new DateTime(2026, 11, 19, 17, 16, 0, DateTimeKind.Utc);
        var signal = new FuturesEmaSignalReadModel { Ema20 = fast, Ema50 = medium, Ema200 = slow, IsWarm = warm,
            Metadata = new() { SignalKey = new(MarketSeriesIdentity.ForContract("ES20261218"), MarketAnalyticsSignalKind.Ema, frame, "ema"),
                IsValid = valid, MarketDataAsOfUtc = new DateTimeOffset(now.AddMinutes(-ageMinutes)) } };
        var snapshot = MarketOutlookComposer.Compose(new MarketOutlookInputState { EntityId = new("ES20261218", new(2026, 11, 19)) },
            MarketOutlookRefreshTrigger.Component, now, fiveMinuteEma: signal, minimumFiveMinuteSourceUtc: now.AddMinutes(-6));
        Assert.Equal(expected, snapshot.FiveMinuteEmaState);
        Assert.Null(snapshot.FuturesEmaSignal); // Daily display baseline is a separate input.
    }
}

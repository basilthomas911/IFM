using FluentAssertions;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Application.ServerManager.UnitTests;
public sealed class MarketOpenSessionTests
{
    static MarketSessionReadModel Valid() => new()
    {
        OperationalValueDate = new(2026, 10, 9), ActiveValueDate = new(2026, 10, 9),
        State = FuturesMarketState.OffTrading, SessionStartUtc = new(2026, 10, 8, 22, 0, 0, DateTimeKind.Utc),
        SessionEndUtc = new(2026, 10, 9, 21, 0, 0, DateTimeKind.Utc), NextTransitionUtc = new(2026, 10, 9, 13, 30, 0, DateTimeKind.Utc),
        Revision = 1, AsOfUtc = new(2026, 10, 8, 22, 0, 1, DateTimeKind.Utc)
    };
    [Fact] public void Evening_session_accepts_next_value_date() => ScheduledMarketOpenReadiness.ValidateSession(Valid());
    [Fact] public void Pending_eod_blocks_feed_start() => ((Action)(() => ScheduledMarketOpenReadiness.ValidateSession(Valid() with { IsEndOfDayPending = true }))).Should().Throw<InvalidOperationException>();
    [Fact] public void Wrong_active_date_blocks_feed_start() => ((Action)(() => ScheduledMarketOpenReadiness.ValidateSession(Valid() with { ActiveValueDate = new(2026, 10, 8) }))).Should().Throw<InvalidOperationException>();
    [Fact] public void Closed_session_blocks_feed_start() => ((Action)(() => ScheduledMarketOpenReadiness.ValidateSession(Valid() with { State = FuturesMarketState.Closed, ActiveValueDate = null }))).Should().Throw<InvalidOperationException>();
    [Fact] public void Invalid_operational_date_blocks_feed_start() => ((Action)(() => ScheduledMarketOpenReadiness.ValidateSession(Valid() with { OperationalValueDate = default }))).Should().Throw<InvalidOperationException>();
}

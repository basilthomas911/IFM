using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Models;

public class TradeRealtimeClientMirrorTests
{
    [Fact]
    public void Copies_server_numbers_even_when_they_differ_from_local_leg_arithmetic()
    {
        var date = new DateOnly(2026, 10, 23);
        var trade = new EstablishedTradeDefinition { Id = new(1, 2, 3, 4), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "option", SignedQuantity = -1 }] };
        var mirror = new TradeRealtimeClientMirror(trade, date);
        var snapshot = mirror.Read() with { MonitoringGeneration = Guid.NewGuid(), SnapshotRevision = 1,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            Summary = new() { NetDelta = 12.34, PositionValue = -1234.56m, NetSpread = -24.69m },
            Spreads = [new() { PutCall = 2, NetSpread = -12.34m, TradePnl = 345.67m }] };
        mirror.AcceptSnapshot(snapshot).Should().BeTrue();
        mirror.Read().Summary.Should().BeSameAs(snapshot.Summary);
        mirror.Read().Spreads.Should().Equal(snapshot.Spreads);
        mirror.AcceptSnapshot(snapshot).Should().BeFalse();
        mirror.AcceptSnapshot(snapshot with { ValueDate = date.AddDays(1), SnapshotRevision = 2 }).Should().BeFalse();
        mirror.Read().Summary.PositionValue.Should().Be(-1234.56m);
    }
}

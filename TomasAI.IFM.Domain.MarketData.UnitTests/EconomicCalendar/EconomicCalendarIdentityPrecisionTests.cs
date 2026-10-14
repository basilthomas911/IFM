using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared;
namespace TomasAI.IFM.Domain.MarketData.UnitTests.EconomicCalendar;
public class EconomicCalendarIdentityPrecisionTests
{
    [Fact]
    public void Full_timestamp_and_projected_timestamp_share_the_existing_second_precision_route_identity()
    {
        var second = new DateTime(2026, 10, 13, 7, 24, 35, DateTimeKind.Utc);
        var full = new EconomicCalendarId(second.AddTicks(4821234), "US", "Identity precision");
        var projected = new EconomicCalendarId(second.AddMilliseconds(482), "US", "Identity precision");
        Assert.Equal(second, full.EventDate);
        Assert.Equal(full, projected);
        Assert.Equal(full.Format(), projected.Format());
        Assert.Equal(full, MessagePackSerializer.Deserialize<EconomicCalendarId>(MessagePackSerializer.Serialize(full)));
        Assert.Equal(full, projected with { EventDate = second.AddTicks(9999999) });
    }
}

using FluentAssertions;
using TomasAI.IFM.UI.Net.Models.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioConcurrencySystemTests
{
    [Theory]
    [Trait("Gate", "PF-07")]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)]
    public void UI_refreshes_stale_edits_instead_of_overwriting(long expected, long current, bool refresh)
        => new PortfolioConcurrencyRefreshModel(expected, current).RequiresRefresh.Should().Be(refresh);
}

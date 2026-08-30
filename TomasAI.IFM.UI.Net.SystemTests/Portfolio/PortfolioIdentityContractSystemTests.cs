using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioIdentityContractSystemTests
{
    [Fact]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void Ui_contract_can_render_and_parse_operator_facing_integer_identity()
    {
        var source = new PortfolioFundOrderTradeId(101, 205, 3001, 4001);
        var rendered = source.Format();
        var parts = rendered.Split('.').Select(int.Parse).ToArray();
        var parsed = new PortfolioFundOrderTradeId(parts[0], parts[1], parts[2], parts[3]);

        rendered.Should().Be("101.205.3001.4001");
        parsed.Should().Be(source);
    }
}

using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.UI.Net.Models.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioIdentityAllocationSystemTests
{
    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public void Successful_allocation_displays_the_exact_integer_identity()
    {
        var model = PortfolioIdentityAllocationModel.Success(new PortfolioId(7001));

        model.IsSuccessful.Should().BeTrue();
        model.DisplayId.Should().Be("7001");
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Category", "Portfolio")]
    public void Failed_allocation_displays_an_error_without_fabricating_an_identity()
    {
        var model = PortfolioIdentityAllocationModel.Failure("Sequence allocation failed.");

        model.IsSuccessful.Should().BeFalse();
        model.PortfolioId.Should().BeNull();
        model.DisplayId.Should().BeEmpty();
        model.Error.Should().Be("Sequence allocation failed.");
    }
}

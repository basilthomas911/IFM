using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioAdministrationUiSystemTests
{
    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public void Administration_screen_uses_the_Funds_visual_vocabulary_and_exposes_the_complete_review_slice()
    {
        using var form = new PortfolioAdministrationForm();

        form.Text.Should().Be("Portfolio Administration");
        form.BackColor.Should().Be(Color.FromArgb(64, 64, 64));
        form.ForeColor.Should().Be(Color.White);
        form.Font.Name.Should().Be("Microsoft Sans Serif");
        form.Font.Size.Should().Be(12F);

        var requiredActions = new[]
        {
            "_refresh", "_createPortfolio", "_newPortfolioVersion", "_portfolioState",
            "_createFund", "_newFundVersion", "_fundState", "_configureAllocation",
            "_configureEnvelope", "_configureAssignment", "_compositions",
        };
        requiredActions.Select(name => Field<Button>(form, name)).Should().OnlyContain(button =>
            !string.IsNullOrWhiteSpace(button.Text) && !string.IsNullOrWhiteSpace(button.AccessibleName));

        new[] { "_portfolios", "_funds", "_allocation", "_envelope", "_assignments" }
            .Select(name => Field<DataGridView>(form, name))
            .Should().OnlyContain(grid => grid.ReadOnly && grid.BackgroundColor == Color.Black &&
                                          !string.IsNullOrWhiteSpace(grid.AccessibleName));
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public void Create_editors_preserve_allocated_integer_ids_and_make_them_read_only()
    {
        using var portfolio = new PortfolioEditorForm(7001);
        using var fund = new FundMandateEditorForm(7001, 8001);

        Field<TextBox>(portfolio, "_id").Text.Should().Be("7001");
        Field<TextBox>(portfolio, "_id").ReadOnly.Should().BeTrue();
        Field<int>(fund, "_portfolioId").Should().Be(7001);
        Field<TextBox>(fund, "_id").Text.Should().Be("8001");
        Field<TextBox>(fund, "_id").ReadOnly.Should().BeTrue();
    }

    static T Field<T>(object owner, string name) =>
        typeof(T) is not null && owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) is T value
            ? value
            : throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}.");
}

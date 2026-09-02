using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioAdministrationUiSystemTests
{
    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Gate", "PF-16")]
    [Trait("Gate", "PF-21")]
    [Trait("Gate", "PF-27")]
    [Trait("Category", "Portfolio")]
    public void Administration_screen_uses_the_Funds_visual_vocabulary_and_exposes_the_complete_review_slice()
    {
        using var form = new PortfolioAdministrationForm();

        form.Text.Should().Be("Portfolio Administration");
        form.BackColor.Should().Be(Color.FromArgb(64, 64, 64));
        form.ForeColor.Should().Be(Color.White);
        form.Font.Name.Should().Be("Microsoft Sans Serif");
        form.Font.Size.Should().Be(10F);

        var menuBar = Field<FlowLayoutPanel>(form, "_menuBar");
        menuBar.BackColor.Should().Be(Color.Black);
        menuBar.ForeColor.Should().Be(Color.White);
        var menuTitle = Field<Label>(form, "_menuTitle");
        menuTitle.Text.Should().Be("Portfolio Administration");
        menuTitle.BackColor.Should().Be(Color.Black);
        menuTitle.ForeColor.Should().Be(Color.White);
        var contentFrame = Field<Panel>(form, "_contentFrame");
        contentFrame.BackColor.Should().Be(Color.Gray);
        contentFrame.Padding.All.Should().Be(3);

        var compactActions = new[] { "_refresh", "_createPortfolio", "_riskPolicy", "_portfolioActions" };
        compactActions.Select(name => Field<Button>(form, name)).Should().OnlyContain(button =>
            !string.IsNullOrWhiteSpace(button.Text) && !string.IsNullOrWhiteSpace(button.AccessibleName));
        Field<ContextMenuStrip>(form, "_portfolioActionsMenu").Items.Cast<ToolStripItem>().Select(x => x.Text)
            .Should().Equal("New Version...", "Change State...", "Delete Draft...");
        form.GetType().GetField("_compositions", BindingFlags.Instance | BindingFlags.NonPublic).Should().BeNull();

        new[] { "_portfolios", "_funds", "_allocation", "_envelope", "_assignments" }
            .Select(name => Field<DataGridView>(form, name))
            .Should().OnlyContain(grid => grid.ReadOnly && grid.BackgroundColor == Color.Black &&
                                          !string.IsNullOrWhiteSpace(grid.AccessibleName));
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Gate", "PF-16")]
    [Trait("Gate", "PF-21")]
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

    [Fact]
    [Trait("Gate", "PF-03")]
    [Trait("Gate", "PF-16")]
    [Trait("Category", "Portfolio")]
    public void Draft_deletion_confirmation_requires_exact_sequence_id_and_non_empty_reason()
    {
        using var dialog = new DeleteDraftPortfolioDialog(new()
        {
            PortfolioId = 7001, Name = "Core Draft", PortfolioVersion = 1,
            OperatingState = TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioOperatingState.Draft,
        });
        var confirmation = Field<TextBox>(dialog, "_confirmation");
        var reason = Field<TextBox>(dialog, "_reason");
        var delete = Field<Button>(dialog, "_delete");

        delete.Enabled.Should().BeFalse();
        confirmation.Text = "7002"; reason.Text = "duplicate";
        delete.Enabled.Should().BeFalse("the generated PortfolioId must match exactly");
        confirmation.Text = "7001";
        delete.Enabled.Should().BeTrue();
        dialog.Reason.Should().Be("duplicate");
    }

    [Fact]
    [Trait("Gate", "PF-23")]
    [Trait("Gate", "PF-27")]
    [Trait("Category", "Portfolio")]
    public void Risk_policy_modal_has_read_only_identity_editable_family_limits_and_bounded_actions()
    {
        using var form = new PortfolioRiskPolicyForm(
            new PortfolioReadModel
            {
                PortfolioId = 7001, Name = "Core", PortfolioVersion = 2,
                OperatingState = PortfolioOperatingState.Active, ActivePolicyId = 9001, ActivePolicyVersion = 3,
                EffectiveFromUtc = DateTime.UtcNow, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "test"
            },
            Substitute.For<IPortfolioQueryApi>(), Substitute.For<IPortfolioIdentityApi>(),
            Substitute.For<IPortfolioFinancialPolicyCommandApi>(), Substitute.For<IReferenceQueryApi>(), true);

        form.Text.Should().Be("Portfolio Risk Policy");
        Field<Label>(form, "_header").Text.Should().Contain("7001").And.Contain("9001 v3");
        Field<TextBox>(form, "_currency").ReadOnly.Should().BeTrue();
        Field<DataGridView>(form, "_policies").ReadOnly.Should().BeTrue();
        Field<DataGridView>(form, "_families").ReadOnly.Should().BeTrue("family rows become editable only inside an explicit edit session");
        new[] { "_newPolicy", "_newVersion", "_save", "_cancel", "_activate", "_retire", "_delete" }
            .Select(name => Field<Button>(form, name))
            .Should().OnlyContain(button => !string.IsNullOrWhiteSpace(button.AccessibleName));
    }

    [Fact]
    [Trait("Gate", "PF-27")]
    [Trait("Category", "Portfolio")]
    public void Risk_policy_family_limit_grid_displays_reference_name_while_preserving_family_id()
    {
        using var form = new PortfolioRiskPolicyForm(
            Portfolio(), Substitute.For<IPortfolioQueryApi>(), Substitute.For<IPortfolioIdentityApi>(),
            Substitute.For<IPortfolioFinancialPolicyCommandApi>(), Substitute.For<IReferenceQueryApi>(), true);
        SetField(form, "_catalog", new[]
        {
            new TradeStrategyFamilyReadModel
            {
                TradeStrategyFamilyId = 2, DefinitionVersion = 1, SystemKey = "VERTICAL_SPREAD", Name = "Vertical Spreads",
                State = TradeStrategyFamilyState.Active, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "test",
            },
        });
        var policy = new PortfolioFinancialPolicyReadModel
        {
            PortfolioId = 7001, PolicyId = 9001, PolicyVersion = 1, Name = "Limits",
            OperatingState = PortfolioFinancialPolicyState.Draft, BaseCurrency = "USD",
            TradeFamilyLimits = [new() { TradeStrategyFamilyId = 2, DefinitionVersion = 1 }],
            EffectiveFromUtc = DateTime.UtcNow, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "test",
        };

        typeof(PortfolioRiskPolicyForm).GetMethod("DisplayPolicy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [policy]);
        var grid = Field<DataGridView>(form, "_families");
        var column = grid.Columns[nameof(TradeFamilyRiskLimitReadModel.TradeStrategyFamilyId)];
        var cell = grid.Rows[0].Cells[column.Index];

        column.HeaderText.Should().Be("Trade Family");
        cell.FormattedValue.Should().Be("Vertical Spreads");
        cell.Value.Should().Be(2, "the immutable policy contract still stores the reference identity");
    }

    [Fact]
    [Trait("Gate", "PF-27")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Unauthorized_policy_journey_is_visible_but_every_mutation_surface_is_read_only()
    {
        using var form = new PortfolioRiskPolicyForm(Portfolio(), Substitute.For<IPortfolioQueryApi>(), Substitute.For<IPortfolioIdentityApi>(),
            Substitute.For<IPortfolioFinancialPolicyCommandApi>(), Substitute.For<IReferenceQueryApi>(), false);

        new[] { "_newPolicy", "_newVersion", "_save", "_cancel", "_activate", "_retire", "_delete" }
            .Select(name => Field<Button>(form, name).Enabled).Should().OnlyContain(x => !x);
        Field<TextBox>(form, "_name").ReadOnly.Should().BeTrue();
        Field<DataGridView>(form, "_families").ReadOnly.Should().BeTrue();
        new[] { "_capital", "_reserve", "_deployable", "_perTrade", "_aggregate", "_margin", "_notional", "_positions", "_drawdown" }
            .Select(name => Field<NumericUpDown>(form, name).Enabled).Should().OnlyContain(x => !x);
    }

    [Fact]
    [Trait("Gate", "PF-27")]
    [Trait("Category", "Portfolio")]
    public async Task Operator_can_edit_save_and_cannot_close_over_unconfirmed_dirty_policy()
    {
        var identities = Substitute.For<IPortfolioIdentityApi>();
        identities.AllocatePolicyIdAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioBusinessIdAllocation>(new()
        {
            Kind = PortfolioBusinessIdentityKind.Policy, Value = 9101, CorrelationId = Guid.NewGuid()
        }));
        var commands = Substitute.For<IPortfolioFinancialPolicyCommandApi>();
        commands.CreatePolicyAsync(Arg.Any<PortfolioFinancialPolicyReadModel>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var queries = Substitute.For<IPortfolioQueryApi>();
        queries.GetPoliciesAsync(7001, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioPage<PortfolioFinancialPolicyReadModel>>(new() { Items = [], PageSize = 200 }));
        var confirmations = 0;
        using var form = new PortfolioRiskPolicyForm(Portfolio(), queries, identities, commands,
            Substitute.For<IReferenceQueryApi>(), true, () => { confirmations++; return false; });
        SetField(form, "_catalog", new[] { new TradeStrategyFamilyReadModel { TradeStrategyFamilyId = 1, DefinitionVersion = 1, SystemKey = "FUTURES", Name = "Futures", State = TradeStrategyFamilyState.Active, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "test" } });

        await InvokeAsync(form, "BeginNewPolicyAsync");
        Field<TextBox>(form, "_name").Text = "Operator limits";
        Field<NumericUpDown>(form, "_capital").Value = 1_000_000;
        Field<NumericUpDown>(form, "_deployable").Value = 900_000;
        Field<NumericUpDown>(form, "_perTrade").Value = 10_000;
        Field<NumericUpDown>(form, "_aggregate").Value = 100_000;
        Field<NumericUpDown>(form, "_margin").Value = 500_000;
        Field<NumericUpDown>(form, "_notional").Value = 5_000_000;
        Field<NumericUpDown>(form, "_positions").Value = 100;
        Field<NumericUpDown>(form, "_drawdown").Value = 200_000;
        Field<DataGridView>(form, "_families").DataSource = new[] { new TradeFamilyRiskLimitReadModel { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 } };
        form.HasUnsavedChanges.Should().BeTrue();

        var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
        typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [closing]);
        closing.Cancel.Should().BeTrue();
        confirmations.Should().Be(1);

        await InvokeAsync(form, "SaveAsync");
        await commands.Received(1).CreatePolicyAsync(
            Arg.Is<PortfolioFinancialPolicyReadModel>(x => x.PortfolioId == 7001 && x.PolicyId == 9101 && x.Name == "Operator limits" && x.TradeFamilyLimits.Single().Enabled),
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        form.HasUnsavedChanges.Should().BeFalse();
    }

    static PortfolioReadModel Portfolio() => new()
    {
        PortfolioId = 7001, Name = "Core", PortfolioVersion = 1, BaseCurrency = "USD", OperatingState = PortfolioOperatingState.Draft,
        EffectiveFromUtc = DateTime.UtcNow, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "test"
    };

    static async Task InvokeAsync(object owner, string method) =>
        await ((Task)(owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(owner, null)
            ?? throw new InvalidOperationException($"Missing {method} on {owner.GetType().Name}.")));

    static void SetField<T>(object owner, string name, T value) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(owner, value);

    static T Field<T>(object owner, string name) =>
        typeof(T) is not null && owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) is T value
            ? value
            : throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}.");
}

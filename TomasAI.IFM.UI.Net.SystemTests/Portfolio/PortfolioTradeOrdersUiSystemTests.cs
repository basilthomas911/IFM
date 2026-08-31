using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioTradeOrdersUiSystemTests
{
    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Trade_Orders_scopes_Portfolio_before_Fund_and_removes_Create_Fund()
    {
        using var form = new TradeOrderEditorForm(Substitute.For<IAppRoot>(), Substitute.For<IReferenceDataService>());

        var portfolio = Field<ComboBox>(form, "_portfolioSelector");
        var fund = Field<ComboBox>(form, "ddlFund");
        var source = Field<ComboBox>(form, "_sourceFilter");
        var createFund = Field<Button>(form, "btnCreateFund");
        var mode = Field<ComboBox>(form, "_historyModeSelector");
        var openLegacy = Field<Button>(form, "btnOpenTrade");
        var tradesPanel = Field<Panel>(form, "pnlTrades");

        portfolio.AccessibleName.Should().Be("Portfolio selector");
        portfolio.Top.Should().BeLessThan(fund.Top);
        source.Items.Cast<string>().Should().Equal("All", "Manual", "Strategy Workflow");
        mode.Items.Cast<string>().Should().Equal("Current", "Legacy History");
        mode.AccessibleName.Should().Be("Trade history mode");
        form.ClientSize.Height.Should().Be(900);
        form.FormBorderStyle.Should().Be(FormBorderStyle.Sizable);
        openLegacy.Parent.Should().BeSameAs(tradesPanel);
        openLegacy.Text.Should().Be("View Legacy Trade...");
        createFund.Visible.Should().BeFalse();
        createFund.Enabled.Should().BeFalse();
        Field<ListView>(form, "lstTradeOrders").Columns.Cast<ColumnHeader>().Select(x => x.Text).Should().Contain("Source");
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void Legacy_trade_opens_or_activates_one_read_only_main_screen_tab()
    {
        using var host = new TabControl();
        var composition = new FundOrderTradeReadModel
        {
            FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            TradeState = TradeState.OrderFilled,
        };
        var history = new LegacyFundTradeHistoryReadModel
        {
            Composition = composition,
            MatchStatus = LegacyTradeMatchStatus.PositionHistory,
        };

        var first = LegacyTradeHistoryTabFactory.OpenOrActivate(host, history);
        var second = LegacyTradeHistoryTabFactory.OpenOrActivate(host, history);

        second.Should().BeSameAs(first);
        host.TabPages.Cast<TabPage>().Should().ContainSingle();
        host.SelectedTab.Should().BeSameAs(first);
        first.Text.Should().Be("1084:1090");
        first.Controls.Cast<Control>().Should().ContainSingle(x => x is LegacyTradeHistoryView);
        ((LegacyTradeHistoryView)first.Controls[0]).IsReadOnly.Should().BeTrue();
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public async Task Legacy_mode_uses_only_legacy_queries_and_disables_every_trade_mutation()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        var queries = Substitute.For<IPortfolioQueryApi>();
        root.Services.Returns(services);
        services.PortfolioQueries.Returns(queries);
        var portfolio = Portfolio(1101, "Legacy Test Portfolio") with { OperatingState = PortfolioOperatingState.Draft };
        var mapping = Fund(1101, 5001, "Imported Legacy Fund") with
        {
            OperatingState = FundOperatingState.Draft,
            HistoricalSource = "FundLegacyDb",
            HistoricalSourceFundId = 1004,
        };
        var legacyFund = new FundReadModel(1004, "Imported Legacy Fund", "history", 0m, false, DateTime.UtcNow, "legacy");
        var legacyOrder = new FundOrderReadModel(1004, 1084, DateTime.UtcNow, TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open, "ES",
            new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 2), "history", DateTime.UtcNow, "legacy", null, string.Empty);
        var composition = new FundOrderTradeReadModel { FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor };
        queries.GetLegacyPortfolioScopesAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyPortfolioScopeReadModel[]>([new() { Portfolio = portfolio, Funds = [mapping] }]));
        queries.GetLegacyFundCatalogAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundHistoryReadModel[]>([new() { Fund = legacyFund, OrderCount = 1, CompositionTradeCount = 1 }]));
        queries.GetLegacyFundOrdersAsync(1004, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), 1000, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundOrderHistoryReadModel[]>([new() { Order = legacyOrder, CompositionTradeCount = 1 }]));
        queries.GetLegacyFundOrderTradesAsync(1004, 1084, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundTradeHistoryReadModel[]>([new() { Composition = composition, MatchStatus = LegacyTradeMatchStatus.NoTradeDbDefinition }]));
        var vm = new TradeOrderEditorViewModel(root, new DateOnly(2026, 8, 30), [], Substitute.For<IReferenceDataService>());
        vm.SetOrderDateRange(new DateTime(2000, 1, 1), DateTime.Today.AddDays(1));

        await vm.SetLegacyHistoryModeAsync(true);
        var trades = await vm.GetLegacyTradesAsync(1084);

        vm.IsLegacyHistoryMode.Should().BeTrue();
        vm.SelectedPortfolio!.Name.Should().Be("Legacy Test Portfolio");
        vm.SelectedFund!.FundId.Should().Be(1004);
        vm.LegacyOrders.Should().ContainSingle();
        trades.Should().ContainSingle();
        vm.CanCreateOrder.Should().BeFalse();
        vm.CanAddTrade.Should().BeFalse();
        vm.CanSubmitOrder.Should().BeFalse();
        await queries.DidNotReceiveWithAnyArgs().GetOrdersAsync(default, default, default, default, default, default);
    }

    [Fact]
    [Trait("Gate", "PF-21")]
    [Trait("Gate", "PF-28")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Obsolete_separate_composition_viewer_is_not_present()
    {
        var assembly = typeof(TradeOrderEditorForm).Assembly;
        assembly.GetType("TomasAI.IFM.UI.Net.Views.Portfolio.PortfolioCompositionForm").Should().BeNull();
    }

    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public async Task Late_Portfolio_response_cannot_replace_the_newer_scope()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        var queries = Substitute.For<IPortfolioQueryApi>();
        root.Services.Returns(services);
        services.PortfolioQueries.Returns(queries);
        var p1 = Portfolio(101, "Portfolio A");
        var p2 = Portfolio(102, "Portfolio B");
        queries.GetPortfoliosAsync(Arg.Any<PortfolioOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<PortfolioPage<PortfolioReadModel>>>(OkPage([p1, p2])));
        var delayed = new TaskCompletionSource<ServiceResult<PortfolioPage<FundMandateReadModel>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var p2Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var p1Calls = 0;
        queries.GetFundsAsync(Arg.Any<int>(), Arg.Any<FundOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var portfolioId = call.ArgAt<int>(0);
                if (portfolioId == 102) { p2Started.TrySetResult(); return delayed.Task; }
                p1Calls++;
                return Task.FromResult<ServiceResult<PortfolioPage<FundMandateReadModel>>>(OkPage([Fund(101, p1Calls == 1 ? 201 : 211, p1Calls == 1 ? "Initial A" : "Latest A")]));
            });
        queries.GetOrdersAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>>(OkPage<FundOrderProjectionReadModel>([])));
        var vm = new TradeOrderEditorViewModel(root, new DateOnly(2026, 8, 30), [], Substitute.For<IReferenceDataService>());
        await vm.LoadFunds();

        var oldScope = vm.SelectPortfolioAsync(1);
        await p2Started.Task;
        var newScope = vm.SelectPortfolioAsync(0);
        await newScope;
        delayed.SetResult(OkPage([Fund(102, 301, "Late B")]));
        await oldScope;

        vm.SelectedPortfolio!.PortfolioId.Should().Be(101);
        vm.Funds.Should().ContainSingle().Which.Name.Should().Be("Latest A");
        vm.Funds.Should().NotContain(x => x.Name == "Late B");
    }

    static PortfolioReadModel Portfolio(int id, string name) => new()
    {
        PortfolioId = id, PortfolioVersion = 1, Name = name, OperatingState = PortfolioOperatingState.Active,
        EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "test",
    };

    static FundMandateReadModel Fund(int portfolioId, int fundId, string name) => new()
    {
        PortfolioId = portfolioId, FundId = fundId, FundMandateVersion = 1, FundCode = $"F{fundId}", Name = name,
        TradingYear = 2026, OperatingState = FundOperatingState.Active, DecisionHorizon = "Daily", Objective = "test",
        UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"], PermittedTradeFamilies = ["Futures"],
        EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "test",
    };

    static ServiceResult<PortfolioPage<T>> OkPage<T>(T[] items) where T : class =>
        new ServiceOk<PortfolioPage<T>>(new() { Items = items, PageSize = 200 });

    static T Field<T>(object owner, string name)
    {
        for (var type = owner.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) is T value)
                return value;
        throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}.");
    }
}

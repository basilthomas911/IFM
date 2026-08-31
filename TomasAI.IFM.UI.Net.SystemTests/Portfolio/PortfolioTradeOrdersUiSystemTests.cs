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

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioTradeOrdersUiSystemTests
{
    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Category", "Portfolio")]
    public void Trade_Orders_scopes_Portfolio_before_Fund_and_removes_Create_Fund()
    {
        using var form = new TradeOrderEditorForm(Substitute.For<IAppRoot>(), Substitute.For<IReferenceDataService>());

        var portfolio = Field<ComboBox>(form, "_portfolioSelector");
        var fund = Field<ComboBox>(form, "ddlFund");
        var source = Field<ComboBox>(form, "_sourceFilter");
        var createFund = Field<Button>(form, "btnCreateFund");

        portfolio.AccessibleName.Should().Be("Portfolio selector");
        portfolio.Top.Should().BeLessThan(fund.Top);
        source.Items.Cast<string>().Should().Equal("All", "Manual", "Strategy Workflow");
        createFund.Visible.Should().BeFalse();
        createFund.Enabled.Should().BeFalse();
        Field<ListView>(form, "lstTradeOrders").Columns.Cast<ColumnHeader>().Select(x => x.Text).Should().Contain("Source");
    }

    [Fact]
    [Trait("Gate", "PF-21")]
    [Trait("Gate", "PF-28")]
    [Trait("Category", "Portfolio")]
    public void Obsolete_separate_composition_viewer_is_not_present()
    {
        var assembly = typeof(TradeOrderEditorForm).Assembly;
        assembly.GetType("TomasAI.IFM.UI.Net.Views.Portfolio.PortfolioCompositionForm").Should().BeNull();
    }

    [Fact]
    [Trait("Gate", "PF-28")]
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

using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;
public sealed class EstablishedIronCondorViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadsExactNewTradeModel_AndRejectsWrongIdentity(bool mismatch)
    {
        var actual = new EstablishedTradeDefinition {
            Id = new(101, 701, 1701, 1101), StrategyKind = TradeStrategyKind.IronCondor,
            EstablishedAtUtc = new DateTime(2026, 10, 6, 23, 19, 0, DateTimeKind.Utc),
            OpeningValue = -13.05m, OpeningCommission = 2.6m,
            Legs = [new TradeLegDefinition { Expiry = new DateOnly(2026,11,20) }]
        };
        var queries = Substitute.For<IEstablishedTradeQueryApi>();
        queries.GetAsync(actual.Id, TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<EstablishedTradeDefinition>(mismatch ? actual with { Id = new(101,701,1601,1001) } : actual));
        var services = Substitute.For<IUiServiceCatalog>(); services.EstablishedTrades.Returns(queries);
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 701 });
        var viewModel = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"), order,
            new PortfolioFundOrderTradeEditorModel { TradeId = 101 }, null, [], establishedTrade: actual);
        if (mismatch)
            await FluentActions.Awaiting(() => viewModel.LoadEstablishedTradeAsync()).Should().ThrowAsync<InvalidOperationException>();
        else
            {
            (await viewModel.LoadEstablishedTradeAsync()).Should().BeSameAs(actual);
            var opening = viewModel.TradeHistory.Should().ContainSingle().Subject;
            opening.OrderId.Should().Be(1701); opening.TradeId.Should().Be(1101);
            opening.TradeStatus.Should().Be(TradeStatus.Open);
            opening.ValueDate.Should().Be(new DateOnly(2026,10,6));
            opening.DaysToExpiry.Should().Be(45);
            opening.NetSpread.Should().Be(-13.05m); opening.Commission.Should().Be(2.6m);
        }
        await queries.Received(1).GetAsync(actual.Id, TradeStrategyKind.IronCondor, Arg.Any<CancellationToken>());
        services.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "get_TradeQueries").Should().BeEmpty();
    }

    [Fact]
    public void Current_position_filters_trade_identity_leg_identity_and_old_generations()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1701, 1101),
            StrategyKind = TradeStrategyKind.IronCondor,
            Legs = ids.Select((id, i) => new TradeLegDefinition { TradeLegId = id, ContractId = $"option-{i}" }).ToArray() };
        var services = Substitute.For<IUiServiceCatalog>();
        services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
            Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
        var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
        var viewModel = new IronCondorViewModel(root,
            new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
            new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
            new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade: trade);
        var position = new StrategyPositionSnapshot { Id = new(trade.Id, Guid.NewGuid()),
            StrategyKind = TradeStrategyKind.IronCondor, PositionSequence = 10, RouteGeneration = 2,
            AsOfUtc = DateTime.UtcNow, Legs = trade.Legs.Select(leg => new StrategyPositionLeg {
                TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = 1, CurrentPrice = 4m }).ToArray() };
        viewModel.ApplyIronCondorPosition(position).Should().BeTrue();
        viewModel.ApplyIronCondorPosition(position).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { RouteGeneration = 1, PositionSequence = 100 }).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { Id = new(new(101, 701, 1701, 9999), position.Id.PositionId) }).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { PositionSequence = 11, Legs = position.Legs[..3] }).Should().BeFalse();
        viewModel.ApplyIronCondorPosition(position with { RouteGeneration = 3, PositionSequence = 1 }).Should().BeTrue();
        viewModel.GetOptionLegContractIds().Should().HaveCount(4);
    }
}

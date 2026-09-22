using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class TradeOrderEditorViewModelTests
{
    [Fact]
    public void UiOrder_DoesNotExposeTradeOwnedFields()
    {
        var canonical = typeof(FundOrderProjectionReadModel).GetProperties()
            .Select(property => property.Name)
            .Except(["UnderlyingRoot", "RequestedTradeDate", "RequestedMaturityDate"])
            .ToHashSet(StringComparer.Ordinal);
        var properties = typeof(PortfolioFundOrderEditorModel).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        properties.Should().Contain(canonical);
        properties.Should().NotContain([
            "UnderlyingRoot",
            "RequestedTradeDate",
            "RequestedMaturityDate",
            "BaseContractId",
        ]);
    }

    [Fact]
    public void UiTrade_CoversCanonicalProjectionAndAddsExecutionContext()
    {
        var canonical = typeof(FundOrderTradeProjectionReadModel).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var ui = typeof(PortfolioFundOrderTradeEditorModel).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        ui.Should().Contain(canonical);
        ui.Should().Contain(["BaseContractId", "HasFillEvidence", "Id"]);
    }

    [Fact]
    public void TradeReference_UsesBaseContractAndCompactTradeWindow()
    {
        FundOrderTradeReference.Create(
                " ESZ26 ",
                new DateOnly(2026, 9, 18),
                new DateOnly(2026, 12, 18))
            .Should().Be("ESZ26 @ 20260918 - 20261218");
    }

    [Fact]
    public async Task AddManualTradeFailure_PublishesCodedPresentationError()
    {
        var commands = Substitute.For<IPortfolioFundCommandApi>();
        commands.AddManualTradeAsync(
                Arg.Any<AddManualFundOrderTradeRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<FundCompositionReservationResult>>(
                new ServiceFailed<FundCompositionReservationResult>(919, "trade reference is required")));
        var services = Substitute.For<IUiServiceCatalog>();
        services.PortfolioFundCommands.Returns(commands);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.Returns(services);
        var viewModel = new TradeOrderEditorViewModel(
            appRoot,
            new DateOnly(2026, 9, 18),
            [],
            Substitute.For<IReferenceDataService>());
        var order = new FundOrderProjectionReadModel
        {
            PortfolioId = 1201,
            FundId = 5401,
            OrderId = 16001,
            AggregateVersion = 1,
        };
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            FundId = 5401,
            OrderId = 16001,
            TradeId = 17001,
            TradeType = TradeType.ShortIronCondor,
            RequestedTradeDate = new DateOnly(2026, 9, 18),
            RequestedMaturityDate = new DateOnly(2026, 9, 18),
            TradeState = TradeState.NewTrade,
            TradeAction = TradeAction.Sell,
            InstructionReference = string.Empty,
            PrimaryTrade = true,
            BaseContractSymbol = "ES",
            BaseContractId = "ESZ26",
        };

        var exception = await FluentActions.Awaiting(() => viewModel.AddManualTradeAsync(order, trade))
            .Should().ThrowAsync<UiServiceOperationException>();

        exception.Which.ErrorCode.Should().Be(919);
        await commands.Received(1).AddManualTradeAsync(
            Arg.Is<AddManualFundOrderTradeRequest>(request =>
                request.BaseContractId == "ESZ26" &&
                request.Reference == "ESZ26 @ 20260918 - 20260918"),
            Arg.Any<CancellationToken>());
        viewModel.LastError.Should().BeEquivalentTo(new
        {
            ErrorCode = 919,
            Message = "trade reference is required",
            Caption = "Add Trade Error",
        });
    }
}

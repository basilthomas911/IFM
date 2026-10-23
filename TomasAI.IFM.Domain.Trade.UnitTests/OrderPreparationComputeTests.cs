using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Order.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public class OrderPreparationComputeTests
{
    [Fact]
    public void Tick_policy_uses_resolved_futures_reference_and_explicit_combo_profile()
    {
        ManualOrderEntryPolicy.Tick("ES",true,.25m,null).Should().Be(.25m);
        ManualOrderEntryPolicy.Tick("ES",false,.25m,null).Should().Be(.05m);
        var config = NSubstitute.Substitute.For<IConfiguration>();
        config["TradeBroker:ManualEntry:VX:OptionComboTick"].Returns("0.10");
        ManualOrderEntryPolicy.Tick("VX",false,null,config).Should().Be(.10m);
        FluentActions.Invoking(() => ManualOrderEntryPolicy.Tick("VX",false,null,null)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ManualOrderEntryPolicy.Tick("ES",true,null,null)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Preparation_contract_round_trips_exact_money_and_selected_quote_evidence()
    {
        var request = Request(2,-2m) with { SelectedQuoteInputs="quote-revision-17" };
        var copy = MessagePack.MessagePackSerializer.Deserialize<TradeOrderPreparationRequest>(MessagePack.MessagePackSerializer.Serialize(request));
        copy.Should().BeEquivalentTo(request);
        var value = TradeOrderPreparationCompute.Calculate(copy,new() { Symbol="ES",Currency="USD" },new(2026,10,23),
            new(2026,10,23,12,0,0,DateTimeKind.Utc),.05m,.65m,default!,"Manual","test");
        MessagePack.MessagePackSerializer.Deserialize<TradeOrderPreparation>(MessagePack.MessagePackSerializer.Serialize(value)).Should().BeEquivalentTo(value);
    }
    [Theory]
    [InlineData(1, -2, -100, 400, 1.30)]
    [InlineData(2, -2, -200, 800, 2.60)]
    public void Credit_vertical_has_cash_sign_quantity_scaling_and_preserved_risk_basis(int quantity, decimal price,
        decimal cash, decimal loss, decimal fee)
    {
        var request = Request(quantity, price);
        var result = TradeOrderPreparationCompute.Calculate(request, new FuturesContractV3ReadModel { Symbol = "ES", Currency = "USD", Exchange = "CME" },
            new(2026, 10, 23), new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc), .05m, .65m, default!, "Manual", "test fee");
        result.CashAmount.Should().Be(cash); result.TotalCommission.Should().Be(fee);
        result.TotalAmount.Should().Be(cash + fee); result.Candidate.MaximumLoss.Should().Be(loss);
        result.Candidate.RequiredCapital.Should().Be(loss); result.Candidate.StressLoss.Should().Be(loss);
        result.Candidate.Components[0].TickIncrement.Should().Be(.05m);
        result.Candidate.ValidUntilUtc.Should().Be(new DateTime(2026, 10, 23, 12, 5, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Off_tick_input_is_not_silently_rounded_for_confirmation()
    {
        var run = () => TradeOrderPreparationCompute.Calculate(Request(1, -2.03m), new(), new(2026, 10, 23),
            DateTime.UtcNow, .05m, .65m, default!, "Manual", "test fee");
        run.Should().Throw<ArgumentException>().WithMessage("*off tick*");
    }

    static TradeOrderPreparationRequest Request(int quantity, decimal price) => new(1, "ES", quantity, new() {
        CompositionId = Guid.NewGuid(), SetupTrade = new() { FundId = 2, OrderId = 3, TradeId = 4 },
        Components = [new TradeOrderComponentDefinition { ComponentId = Guid.NewGuid(), StrategyKind = TradeStrategyKind.VerticalSpread,
            SignedNetDebitLimit = price, Legs = [
                new() { TradeLegId = Guid.NewGuid(), ContractId = "put-short", AssetFamily = TradeAssetFamily.FuturesOption, PutCall = 2, Strike = 6000, SignedQuantity = -quantity, CashMultiplier = 50 },
                new() { TradeLegId = Guid.NewGuid(), ContractId = "put-long", AssetFamily = TradeAssetFamily.FuturesOption, PutCall = 2, Strike = 5990, SignedQuantity = quantity, CashMultiplier = 50 }] }.ToPortfolioComponent()] });
}

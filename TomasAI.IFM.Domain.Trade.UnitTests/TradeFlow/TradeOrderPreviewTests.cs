using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Order.Query;
using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class TradeOrderPreviewTests
{
    static TradeOrderPreviewRequest Condor(int quantity = 1) => new(7,
        [new("SC", -1, quantity, 110, true, false, 2, 2.4m, .16, .2, 50),
         new("LC", 1, quantity, 120, true, false, .5m, .7m, .10, .1, 50),
         new("SP", -1, quantity, 90, false, false, 2, 2.4m, -.16, .2, 50),
         new("LP", 1, quantity, 80, false, false, .5m, .7m, -.10, .1, 50)], quantity, null, .05m, null);

    [Theory]
    [InlineData(1, 160, 340)] [InlineData(2, 320, 680)] [InlineData(10, 1600, 3400)]
    public void Condor_totals_use_signed_prices_and_actual_quantities(int quantity, decimal profit, decimal loss)
    {
        var preview = TradeOrderPreviewCompute.Create(Condor(quantity));
        Assert.Equal(7, preview.InputRevision);
        Assert.Equal(-3.8m, preview.NetBid); Assert.Equal(-2.6m, preview.NetAsk);
        Assert.Equal(-3.2m, preview.NetMid); Assert.Equal(-3.2m, preview.OrderPrice);
        Assert.Equal(profit, preview.MaximumProfit); Assert.Equal(loss, preview.MaximumLoss);
        Assert.Equal(profit, preview.EstimatedCredit); Assert.Equal(0d, preview.NetDelta);
        Assert.Equal(-10d * quantity, preview.NetVega);
        Assert.Equal(10m, preview.CallWidth); Assert.Equal(10m, preview.PutWidth);
        Assert.True(preview.HasPayoffEstimate); Assert.True(preview.HasBalancedQuantities);
    }

    [Fact]
    public void Entered_price_and_quantity_changes_recompute_payoff_without_changing_quotes()
    {
        var request = Condor(2) with { EnteredOrderPrice = -3.15m };
        var preview = TradeOrderPreviewCompute.Create(request);
        Assert.Equal(-3.15m, preview.OrderPrice); Assert.Equal(315m, preview.MaximumProfit);
        Assert.Equal(685m, preview.MaximumLoss); Assert.Equal(-3.2m, preview.NetMid);
    }

    [Fact]
    public void Both_vertical_directions_have_expected_cash_payoffs()
    {
        var credit = Condor() with { SelectedLegs = Condor().SelectedLegs.Take(2).ToArray() };
        var preview = TradeOrderPreviewCompute.Create(credit);
        Assert.Equal(80m, preview.MaximumProfit); Assert.Equal(420m, preview.MaximumLoss);
        var debit = credit with { SelectedLegs = credit.SelectedLegs.Select(x => x with { Side = -x.Side }).ToArray() };
        preview = TradeOrderPreviewCompute.Create(debit);
        Assert.Equal(420m, preview.MaximumProfit); Assert.Equal(80m, preview.MaximumLoss);
    }

    [Fact]
    public void Incomplete_quotes_or_risk_inputs_stay_unavailable()
    {
        var request = Condor();
        request.SelectedLegs[0] = request.SelectedLegs[0] with { Bid = null };
        var preview = TradeOrderPreviewCompute.Create(request);
        Assert.False(preview.HasCompleteQuotes); Assert.Null(preview.OrderPrice); Assert.Null(preview.MaximumLoss);
        request = Condor(); request.SelectedLegs[0] = request.SelectedLegs[0] with { Delta = null, Vega = null, CashMultiplier = null };
        preview = TradeOrderPreviewCompute.Create(request);
        Assert.True(preview.HasCompleteQuotes); Assert.Null(preview.NetDelta); Assert.Null(preview.NetVega);
        Assert.False(preview.HasPayoffEstimate); Assert.Null(preview.EstimatedCredit);
        Assert.False(TradeOrderPreviewCompute.Create(Condor() with { SelectedLegs = [] }).HasCompleteQuotes);
    }

    [Fact]
    public void Unequal_leg_quantities_keep_actual_exposure_and_report_unbounded_call_risk()
    {
        var request = Condor(); request.SelectedLegs[0] = request.SelectedLegs[0] with { Quantity = 2 };
        var preview = TradeOrderPreviewCompute.Create(request);
        Assert.False(preview.HasBalancedQuantities); Assert.True(preview.HasUnlimitedLoss); Assert.Null(preview.MaximumLoss);
        var longCall = request with { SelectedLegs = [request.SelectedLegs[1]] };
        Assert.True(TradeOrderPreviewCompute.Create(longCall).HasUnlimitedProfit);
    }

    [Theory] [InlineData(1)] [InlineData(-1)]
    public void Futures_reference_and_entered_limit_do_not_fabricate_bid_ask(int side)
    {
        var request = new TradeOrderPreviewRequest(2, [new("ES", side, 2, null, false, true, null, null, null, null, 50)], 2, null, .25m, 7800);
        var preview = TradeOrderPreviewCompute.Create(request);
        Assert.Equal(side * 7800m, preview.OrderPrice); Assert.Equal(side * 100d, preview.NetDelta);
        Assert.Null(preview.NetBid); Assert.Null(preview.NetAsk); Assert.False(preview.HasPayoffEstimate);
        preview = TradeOrderPreviewCompute.Create(request with { UnderlyingReferencePrice = null, EnteredOrderPrice = 7801 });
        Assert.Equal(7801m, preview.OrderPrice); Assert.Null(preview.NetMid);
    }

    [Fact]
    public void Rounding_uses_the_supplied_combo_tick_and_away_from_zero()
    {
        var request = Condor() with { TickIncrement = .25m };
        Assert.Equal(-3.25m, TradeOrderPreviewCompute.Create(request).OrderPrice);
    }

    [Theory] [InlineData("quantity")] [InlineData("duplicate")] [InlineData("tick")] [InlineData("crossed")]
    [InlineData("nan")] [InlineData("infinity")] [InlineData("side")] [InlineData("multiplier")]
    public void Invalid_drafts_are_rejected_before_calculation(string invalid)
    {
        var request = Condor();
        request = invalid switch { "quantity" => request with { StrategyQuantity = 0 }, "tick" => request with { TickIncrement = 0 }, _ => request };
        request.SelectedLegs[0] = invalid switch {
            "duplicate" => request.SelectedLegs[1], "crossed" => request.SelectedLegs[0] with { Ask = 1 },
            "nan" => request.SelectedLegs[0] with { Delta = double.NaN }, "infinity" => request.SelectedLegs[0] with { Vega = double.PositiveInfinity },
            "side" => request.SelectedLegs[0] with { Side = 0 }, "multiplier" => request.SelectedLegs[0] with { CashMultiplier = -1 }, _ => request.SelectedLegs[0] };
        Assert.Throws<ArgumentException>(() => TradeOrderPreviewCompute.Create(request));
    }

    [Fact]
    public void Aggregate_greek_overflow_cannot_publish_infinite_values()
    {
        var request = Condor(); request.SelectedLegs[0] = request.SelectedLegs[0] with { Delta = double.MaxValue };
        Assert.Throws<ArgumentException>(() => TradeOrderPreviewCompute.Create(request));
    }

    [Fact]
    public async Task Query_resolves_tick_reference_and_replies_without_mutating_state()
    {
        var context = Substitute.For<ITradeOrderQueryContext>();
        var db = Substitute.For<TomasAI.IFM.Application.Storage.IDbContextFactory>();
        var securities = Substitute.For<TomasAI.IFM.Application.Storage.SecuritiesDb.ISecuritiesDbContext>();
        db.SecuritiesDb.Returns(securities); context.DbFactory.Returns(db);
        securities.GetFuturesContractAsync("ES", Arg.Any<CancellationToken>()).Returns(
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel("ES","ESZ6","ES","ESZ6","FUT","USD","XCME","50",new(2026,12,18),true,true) { TickSize=.25m });
        var future = Condor() with { SelectedLegs=[new("ES",1,1,null,false,true,10m,10.25m,null,null,50m)] };
        var query = new GetTradeOrderPreviewQuery { Subject = new(ActorType.Query, GetTradeOrderPreviewQuery.Actor, GetTradeOrderPreviewQuery.Verb, "Preview"), TradeOrderPreviewRequest = future };
        await query.ExecuteAsync(context, CancellationToken.None);
        context.Received().ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, Arg.Is<ServiceResult<TradeOrderPreview>>(x => x.Success && x.Value!.ResolvedTickIncrement == .25m));
        var invalid = query with { TradeOrderPreviewRequest = future with { StrategyQuantity = 0 } };
        await invalid.ExecuteAsync(context, CancellationToken.None);
        context.Received().ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, Arg.Is<ServiceResult<TradeOrderPreview>>(x => !x.Success && x.ErrorCode == 400));
        await securities.Received(2).GetFuturesContractAsync("ES", Arg.Any<CancellationToken>());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.ExecuteAsync(context, cancelled.Token).AsTask());
    }

    [Fact]
    public void Query_and_result_round_trip_permanent_numeric_keys()
    {
        var query = new GetTradeOrderPreviewQuery { Subject = new(ActorType.Query, GetTradeOrderPreviewQuery.Actor, GetTradeOrderPreviewQuery.Verb, "Preview"), TradeOrderPreviewRequest = Condor(10) };
        var restored = MessagePackSerializer.Deserialize<GetTradeOrderPreviewQuery>(MessagePackSerializer.Serialize(query));
        Assert.Equal(query.TradeOrderPreviewRequest.SelectedLegs, restored.TradeOrderPreviewRequest.SelectedLegs);
        var preview = TradeOrderPreviewCompute.Create(restored.TradeOrderPreviewRequest);
        Assert.Equal(preview, MessagePackSerializer.Deserialize<TradeOrderPreview>(MessagePackSerializer.Serialize(preview)));
    }
}

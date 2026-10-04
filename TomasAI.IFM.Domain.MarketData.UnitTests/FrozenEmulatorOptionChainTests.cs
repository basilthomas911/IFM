using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class FrozenEmulatorOptionChainTests
{
    [Fact]
    public void Frozen_preview_calculates_opposing_deltas_and_marks_quotes_stale()
    {
        var at = new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero);
        var expiry = at.AddDays(7);
        var call = Contract("C", expiry);
        var put = Contract("P", expiry);

        var callValue = FrozenEmulatorOptionChain.Evaluate(call, null, 5000m, 50m, at);
        var putValue = FrozenEmulatorOptionChain.Evaluate(put, null, 5000m, 50m, at);

        Assert.True(callValue.Delta is > 0 and < 1);
        Assert.True(putValue.Delta is < 0 and > -1);
        Assert.True(callValue.Bid is > 0 && callValue.Ask > callValue.Bid);
        Assert.True(callValue.IsStale);
        Assert.True(callValue.GreeksValid);
        Assert.True(callValue.Vega > 0);
        Assert.True(putValue.Vega > 0);
        Assert.Null(callValue.Volume);
        Assert.Null(callValue.OpenInterest);
    }

    [Fact]
    public void Frozen_preview_keeps_observed_option_bid_and_ask()
    {
        var at = new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero);
        var stored = new FuturesOptionTickDataV2ReadModel
        {
            ContractId = "ES-C-5000", BidPrice = 78.25, AskPrice = 79.25,
            BidSize = 4, AskSize = 6, ImpliedVolatility = 0.18,
            Volume = 0, OpenInterest = 1234,
            VolumeValueDate = new DateOnly(2026, 10, 1),
            OpenInterestValueDate = new DateOnly(2026, 10, 2)
        };

        var value = FrozenEmulatorOptionChain.Evaluate(Contract("C", at.AddDays(7)),
            stored, 5000m, 50m, at);

        Assert.Equal(78.25m, value.Bid);
        Assert.Equal(79.25m, value.Ask);
        Assert.Equal(0, value.Volume);
        Assert.Equal(1234, value.OpenInterest);
        Assert.Equal(stored.VolumeValueDate, value.VolumeValueDate);
        Assert.Equal(stored.OpenInterestValueDate, value.OpenInterestValueDate);
        Assert.True(value.Delta is > 0 and < 1);
        Assert.True(value.IsStale);
    }

    [Fact]
    public void Frozen_preview_solves_volatility_from_an_observed_quote_when_tick_iv_is_missing()
    {
        var at = new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero);
        var stored = new FuturesOptionTickDataV2ReadModel
        {
            ContractId = "ES-C-5000", BidPrice = 78.25, AskPrice = 79.25
        };

        var value = FrozenEmulatorOptionChain.Evaluate(Contract("C", at.AddDays(7)),
            stored, 5000m, 50m, at);

        Assert.True(value.ImpliedVolatility > 0.2);
        Assert.True(value.Delta is > 0 and < 1);
    }

    [Fact]
    public void Frozen_preview_still_calculates_a_model_delta_without_a_stored_deviation()
    {
        var at = new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero);

        var value = FrozenEmulatorOptionChain.Evaluate(Contract("P", at.AddDays(7)),
            null, 5000m, null, at);

        Assert.Equal(0.20, value.ImpliedVolatility);
        Assert.True(value.Delta is < 0 and > -1);
        Assert.True(value.IsStale);
    }

    private static FuturesOptionContractReadModel Contract(string side, DateTimeOffset expiry) => new()
    {
        ContractId = $"ES-{side}-5000", StrikePrice = 5000,
        OptionType = side == "C" ? "Call" : "Put", ExpirationUtc = expiry,
        TickSize = 0.25m
    };
}

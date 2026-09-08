using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class FuturesOptionContractIdTests
{
    [Theory]
    [InlineData("ES20260910C6500", "ES", 6500, OptionType.Call)]
    [InlineData("ES20260910P10000", "ES", 10000, OptionType.Put)]
    [InlineData("NQ20260910C25000", "NQ", 25000, OptionType.Call)]
    [InlineData("6E20260910P0100", "6E", 100, OptionType.Put)]
    public void Variable_width_strikes_preserve_symbol_date_right_and_business_key(string value, string symbol, int strike, OptionType right)
    {
        var id = new FuturesOptionContractId(value);
        Assert.Equal(symbol, id.Symbol); Assert.Equal(new DateTime(2026, 9, 10), id.MaturityDate);
        Assert.Equal(strike, id.StrikePrice); Assert.Equal(right, id.OptionType); Assert.Equal(value, id.Format());
    }

    [Theory]
    [InlineData("ES20260910X6500")] [InlineData("ES20260230C6500")]
    [InlineData("ES20260910C0000")] [InlineData("ES20260910C-100")]
    [InlineData("ES20260910C6500.5")] [InlineData("ES20260910C2147483648")]
    public void Invalid_components_are_rejected(string value)
        => Assert.Throws<InvalidOperationException>(() => new FuturesOptionContractId(value));
}

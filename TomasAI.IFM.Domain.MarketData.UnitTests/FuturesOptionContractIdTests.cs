using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using System.Globalization;
using MessagePack;

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
    [InlineData("ES20260910C6500,5")] [InlineData("ES20260910C6500.5.1")]
    public void Invalid_components_are_rejected(string value)
        => Assert.Throws<InvalidOperationException>(() => new FuturesOptionContractId(value));

    [Theory]
    [InlineData("6500.5")]
    [InlineData("0.000000001")]
    [InlineData("2147483648")]
    [InlineData("79228162514264337593543950335")]
    public void Fractional_and_large_strikes_are_exact_decimals(string strikeText)
    {
        var strike = decimal.Parse(strikeText, CultureInfo.InvariantCulture);
        var value = FuturesOptionContractId.Create("ES", new(2026, 9, 18), OptionType.Call, strike);
        var parsed = new FuturesOptionContractId(value);
        Assert.Equal(strike, parsed.StrikePrice);
        Assert.Equal("ES20260918C" + strikeText, parsed.ContractId);
    }

    [Fact]
    public void Canonical_format_is_invariant_and_preserves_existing_ids()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal("ES20260918C6500.5", FuturesOptionContractId.Create("ES", new(2026, 9, 18), OptionType.Call, 6500.50m));
            Assert.Equal("ES20260918C6500", FuturesOptionContractId.Create("ES", new(2026, 9, 18), OptionType.Call, 6500.00m));
            var saved = new FuturesOptionContractId("6E20260918P0100");
            Assert.Equal("6E20260918P0100", saved.Format());
            Assert.Equal(100m, saved.StrikePrice);
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void Decimal_entity_round_trips_without_splitting_the_fraction_from_the_contract()
    {
        var entity = new FuturesOptionContractEntityId("ES20260918C6500.5", 2026);
        var subject = new ActorSubject(ActorType.Command, "FuturesOptionContract", "Add", entity.Format());
        Assert.Equal(subject, subject.ToString().ToSubject());
        Assert.Equal("ES20260918C6500.5.2026", subject.EntityId);
        Assert.Equal(entity, MessagePackSerializer.Deserialize<FuturesOptionContractEntityId>(MessagePackSerializer.Serialize(entity)));
    }

    [Theory]
    [InlineData(".5")]
    [InlineData("6500.")]
    [InlineData("+6500")]
    [InlineData("6.5e3")]
    [InlineData("6500.50000000000000000000000001")]
    [InlineData("79228162514264337593543950336")]
    public void Noncanonical_numeric_syntax_and_precision_loss_are_rejected(string strike)
        => Assert.Throws<InvalidOperationException>(() => new FuturesOptionContractId("ES20260918C" + strike));
}

using FluentAssertions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class OptionRefreshAlgorithmsTests
{
    static readonly FuturesContractV3ReadModel[] Futures =
    [
        Contract("ESH7", new DateOnly(2027, 3, 19)),
        Contract("ESM7", new DateOnly(2027, 6, 18)),
        Contract("ESU7", new DateOnly(2027, 9, 17))
    ];

    [Theory]
    [InlineData(2027, 1, 1, "ESH7")]
    [InlineData(2027, 3, 19, "ESH7")]
    [InlineData(2027, 3, 20, "ESM7")]
    [InlineData(2027, 12, 31, "ESU7")]
    public void Underlying_lookup_returns_first_contract_not_before_expiry(
        int year,
        int month,
        int day,
        string expected)
    {
        OptionRefreshAlgorithms.FindUnderlyingContractId(
                Futures,
                new DateOnly(year, month, day))
            .Should().Be(expected);
    }

    [Fact]
    public void Underlying_lookup_rejects_an_empty_curve()
    {
        var action = () => OptionRefreshAlgorithms.FindUnderlyingContractId(
            Array.Empty<FuturesContractV3ReadModel>(),
            new DateOnly(2027, 1, 1));

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(17, 4)]
    [InlineData(4, 0)]
    [InlineData(4, 17)]
    public void Refresh_options_reject_invalid_concurrency(
        int providerConcurrency,
        int verificationConcurrency)
    {
        var options = new OptionContractExpiryCalendarOptions
        {
            MaximumProviderConcurrency = providerConcurrency,
            MaximumVerificationConcurrency = verificationConcurrency
        };

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>();
    }

    static FuturesContractV3ReadModel Contract(string id, DateOnly lastTradeDate) =>
        new(id, id, "ES", id, "FUT", "USD", "CME", "50", lastTradeDate, false);
}

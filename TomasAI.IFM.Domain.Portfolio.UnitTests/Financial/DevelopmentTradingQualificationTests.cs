using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

public sealed class DevelopmentTradingQualificationTests
{
    [Theory]
    [InlineData(true, "Emulator", true)]
    [InlineData(false, "Emulator", false)]
    [InlineData(true, "Live", false)]
    [InlineData(true, "Paper", false)]
    public void Only_development_emulator_qualifications_are_exempt(bool development, string environment, bool exempt)
    {
        var expired = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var book = new FinancialBookConfiguration
        {
            Environment = environment, MigrationQualified = false,
            Funds = [new() { FundId = 701, CanSpend = false, Reference = new() { ValidUntilUtc = expired },
                Deployments = [new(new() { ValidUntilUtc = expired }, [], 10000)] }]
        };
        var result = new FinancialDevelopmentPolicy(development).TradingBook(book);
        result.DevelopmentQualificationsExempt.Should().Be(exempt);
        result.MigrationQualified.Should().Be(exempt);
        result.Funds[0].CanSpend.Should().Be(exempt);
        if (exempt)
        {
            result.Funds[0].Reference.ValidUntilUtc.Should().BeAfter(DateTime.UtcNow);
            result.Funds[0].Deployments[0].Reference.ValidUntilUtc.Should().BeAfter(DateTime.UtcNow);
        }
        else result.Funds[0].Reference.ValidUntilUtc.Should().Be(expired);
        result.Funds[0].Deployments[0].MaximumRiskPerTrade.Should().Be(10000);
        book.MigrationQualified.Should().BeFalse();
        book.Funds[0].CanSpend.Should().BeFalse();
        book.Funds[0].Reference.ValidUntilUtc.Should().Be(expired);
        MessagePackSerializer.Deserialize<FinancialBookConfiguration>(MessagePackSerializer.Serialize(result))
            .DevelopmentQualificationsExempt.Should().BeFalse();
    }
}

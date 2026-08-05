using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public class YieldCurveRateValidationRulesTests
{
    [Fact]
    public async Task CachedValidatorSupportsConcurrentActorValidation()
    {
        var rules = new YieldCurveRateValidationRules();
        var rate = new YieldCurveRateReadModel(
            new DateOnly(2026, 8, 5),
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

        var validations = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                for (var iteration = 0; iteration < 100; iteration++)
                    rules.Execute(rate).Should().BeEmpty();
            }));

        await Task.WhenAll(validations);
    }
}

using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Shared;

public class ValidationConcurrencyTests
{
    [Fact]
    public async Task CachedValidatorSupportsConcurrentExecution()
    {
        var rules = new FuturesRsiSignalEntityIdValidationRules();
        var entityId = new FuturesRsiSignalEntityId(
            "ESU6",
            new DateOnly(2026, 8, 5),
            TimeFrameType.Daily,
            14);

        var validationTasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                for (var iteration = 0; iteration < 250; iteration++)
                    Assert.Empty(rules.Execute(entityId));
            }));

        await Task.WhenAll(validationTasks);
    }
}

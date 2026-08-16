namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

/// <summary>
/// ITI pipeline tests share the same local NATS durable consumers and integration
/// databases, so their actor hosts must not compete with one another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ItiPipelineIntegrationCollection
{
    public const string Name = "ITI pipeline integration";
}

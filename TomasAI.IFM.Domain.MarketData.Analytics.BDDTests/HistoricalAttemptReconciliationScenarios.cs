using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests;

public sealed class HistoricalAttemptReconciliationScenarios
{
    [Fact]
    public void Given_a_failed_attempt_when_retry_completes_then_redelivery_preserves_one_completion()
    {
        var id = new FuturesAnalyticsHistoricalDataLoaderEntityId(Guid.NewGuid());
        var state = new FuturesAnalyticsHistoricalDataLoaderCommandState();
        var load = new LoadFuturesAnalyticsHistoricalDataCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, LoadFuturesAnalyticsHistoricalDataCommand.Actor,
                LoadFuturesAnalyticsHistoricalDataCommand.Verb, id.Format())
        };
        var fail = new FailFuturesAnalyticsHistoricalDataLoaderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, ErrorMessage = "provider unavailable",
            Subject = new(ActorType.Command, FailFuturesAnalyticsHistoricalDataLoaderCommand.Actor,
                FailFuturesAnalyticsHistoricalDataLoaderCommand.Verb, id.Format())
        };
        var complete = new CompleteFuturesAnalyticsHistoricalDataLoaderCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id, RequestSha256 = "qualified",
            CompletedAtUtc = DateTime.UtcNow,
            Subject = new(ActorType.Command, CompleteFuturesAnalyticsHistoricalDataLoaderCommand.Actor,
                CompleteFuturesAnalyticsHistoricalDataLoaderCommand.Verb, id.Format())
        };

        Assert.True(load.Execute(state).Success);
        Assert.True(fail.Execute(state).Success);
        Assert.True(state.IsFailed);
        Assert.True(complete.Execute(state).Success);
        Assert.True(complete.Execute(state).Success);
        Assert.True(fail.Execute(state).Success);

        Assert.True(state.IsCompleted);
        Assert.False(state.IsFailed);
        Assert.Collection(state.Events,
            item => Assert.IsType<FuturesAnalyticsHistoricalDataLoaderRequestedEvent>(item),
            item => Assert.IsType<FuturesAnalyticsHistoricalDataLoaderFailedEvent>(item),
            item => Assert.IsType<FuturesAnalyticsHistoricalDataLoaderCompletedEvent>(item));
    }
}

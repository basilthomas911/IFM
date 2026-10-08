using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost.PortableTests;

public sealed class ScheduledMarketOpenReadinessTests
{
    [Theory]
    [InlineData("Healthy", true)]
    [InlineData("Stopped", false)]
    [InlineData("RuntimeDate", false)]
    [InlineData("ReadinessDate", false)]
    [InlineData("NoGeneration", false)]
    [InlineData("CoreNotReady", false)]
    [InlineData("ProducerDead", false)]
    [InlineData("WorkerDead", false)]
    [InlineData("NoSubscriptions", false)]
    [InlineData("MissingSubscription", false)]
    [InlineData("OtherDataset", false)]
    public void Opening_requires_a_healthy_subscribed_generation_for_its_admitted_date(string fault, bool expected)
    {
        var date = new DateOnly(2026,10,8);
        var runtime = new MarketDataFeedRuntimeStatusReadModel { IsRunning = fault != "Stopped", ActiveValueDate = fault == "RuntimeDate" ? date.AddDays(-1) : date };
        var readiness = new DatabentoReadinessReadModel {
            CoreReady = fault != "CoreNotReady", ValueDate = fault == "ReadinessDate" ? date.AddDays(-1) : date,
            NativeGeneration = fault == "NoGeneration" ? Guid.Empty : Guid.NewGuid(),
            Feeds = [new() { Dataset = fault == "OtherDataset" ? "OTHER" : "GLBX.MDP3", ProducerAlive = fault != "ProducerDead",
                AggregationWorkerRunning = fault != "WorkerDead", ExpectedSubscriptions = fault == "NoSubscriptions" ? 0 : 2,
                ReceivedSubscriptions = fault == "MissingSubscription" ? 1 : 2 }]
        };
        Assert.Equal(expected, ScheduledMarketOpenReadiness.IsReady(runtime, readiness, date));
    }
    [Fact]
    public void Missing_runtime_or_readiness_cannot_report_business_completion()
    {
        Assert.False(ScheduledMarketOpenReadiness.IsReady(null, new(), new(2026,10,8)));
        Assert.False(ScheduledMarketOpenReadiness.IsReady(new(), null, new(2026,10,8)));
    }
}

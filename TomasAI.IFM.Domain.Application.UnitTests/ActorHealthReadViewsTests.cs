using TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class ActorHealthReadViewsTests
{
    [Fact]
    public void Summary_and_actor_pages_preserve_authoritative_red_status_and_stable_actor_identity()
    {
        var actors = Enumerable.Range(0, 3).Select(index => new SupervisorActorSnapshot(new(ActorType.Command, "Actor" + index),
            "Domain", "Implementation", true, SupervisorActorLifecycleState.Running, 1, SupervisorActorHealthStatus.Red,
            5, 1, 1, 1, 0, 0, 0, 0, [])).ToArray();
        var snapshot = new SupervisorRuntimeSnapshot(DateTime.UtcNow, SupervisorActorHealthStatus.Red, 3, 3, 0, 15, actors, [], [], []);
        var summary = ActorHealthReadViews.Summarize(snapshot);
        Assert.Equal(SupervisorActorHealthStatus.Red, summary.OverallStatus);
        Assert.Equal(15, summary.QueuedMessageCount);
        var page = ActorHealthReadViews.Actors(snapshot, 1, 1);
        Assert.Equal(3, page.Total); Assert.Equal(actors[1].ActorId, Assert.Single(page.Rows).ActorId);
        Assert.Equal(SupervisorActorHealthStatus.Red, page.Rows[0].Status);
        Assert.Empty(ActorHealthReadViews.Actors(snapshot, 3, 1).Rows);
        Assert.Throws<ArgumentOutOfRangeException>(() => ActorHealthReadViews.Actors(snapshot, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActorHealthReadViews.Actors(snapshot, 0, 257));
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        TomasAI.IFM.Application.Api.Server.Core.Http.Serialization.ApiServerJson.Configure(options);
        using var serialized = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(summary, options));
        Assert.Equal("Red", serialized.RootElement.GetProperty("overallStatus").GetString());
        using var serializedPage = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(page, options));
        Assert.Equal(1, serializedPage.RootElement.GetProperty("rows").GetArrayLength());
    }
}

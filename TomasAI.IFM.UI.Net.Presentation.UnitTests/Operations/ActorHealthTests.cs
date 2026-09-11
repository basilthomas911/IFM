using System.Net;
using System.Net.Http.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.UI.Net.Services.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Operations;

public sealed class ActorHealthTests
{
    [Fact]
    public async Task SupervisorSnapshot_DeserializesIntoUiOwnedActorTreeContract()
    {
        var now = DateTime.UtcNow;
        var thread = new ActorThreadId(ActorType.Event, "OrderProjector", "fund-1");
        var mailbox = new ActorMailboxMetricsSnapshot(
            thread, 3, 10, 7, 6, 1, 0, 0, 0, true, ActorMailboxLifecycleState.Running, 1, true, "Project", now, now, now, now,
            typeof(InvalidOperationException).FullName!, "projection failed", 8);
        var actor = new SupervisorActorSnapshot(
            thread.MailboxId, "TomasAI.IFM.Domain.Trade", "OrderProjector", true,
            SupervisorActorLifecycleState.Running, 1,
            SupervisorActorHealthStatus.Yellow, 3, 10, 7, 6, 1, 0, 0, 0, [mailbox]);
        var backend = new SupervisorRuntimeSnapshot(
            now, SupervisorActorHealthStatus.Yellow, 1, 1, 1, 3, [actor], [], []);
        using var client = new HttpClient(new Reply(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(backend)
        }));
        using var service = new ActorHealthQueryService(client, new Uri("http://localhost/api/actor-health"));

        var result = await service.GetAsync(now.AddHours(-1), now);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.QueuedMessageCount);
        Assert.Equal("OrderProjector", Assert.Single(result.Value.Actors).ActorId.Name);
        Assert.Equal("fund-1", Assert.Single(result.Value.Actors[0].Mailboxes).ThreadId.EntityId);
        Assert.Equal("projection failed", result.Value.Actors[0].Mailboxes[0].LastError);
    }

    [Fact]
    public async Task InvalidDateRange_DoesNotCallEndpoint()
    {
        var called = false;
        using var client = new HttpClient(new Reply(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var service = new ActorHealthQueryService(client, new Uri("http://localhost/api/actor-health"));

        var result = await service.GetAsync(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-1));

        Assert.False(result.IsSuccess);
        Assert.False(called);
    }

    sealed class Reply(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(reply(request));
    }
}

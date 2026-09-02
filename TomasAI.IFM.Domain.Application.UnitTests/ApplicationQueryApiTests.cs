using System.Reflection;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationQueryApiTests
{
    [Fact]
    public async Task Current_status_uses_the_typed_Application_query_subject_and_returns_the_snapshot()
    {
        var expected = new ApplicationStartupStatus
        {
            State = ApplicationLifecycleState.Running,
            ValueDate = new DateOnly(2026, 9, 2),
            ProcessBootId = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            StartedAtUtc = new DateTime(2026, 9, 2, 22, 0, 0, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 9, 2, 22, 0, 1, DateTimeKind.Utc),
            Summary = "Application startup completed."
        };
        var producer = DispatchProxy.Create<IActorProducer, ActorProducerProxy>();
        var proxy = (ActorProducerProxy)(object)producer;
        proxy.Expected = expected;

        var result = await new ApplicationQueryApi(producer).GetStartupStatusAsync();

        Assert.True(result.Success);
        Assert.Same(expected, result.Value);
        var query = Assert.IsType<GetApplicationStartupStatusQuery>(proxy.Query);
        Assert.True(query.Subject.Is(
            ActorType.Query,
            GetApplicationStartupStatusQuery.Actor,
            GetApplicationStartupStatusQuery.Verb));
        Assert.Equal("current", query.Subject.EntityId);
    }

    public class ActorProducerProxy : DispatchProxy
    {
        public ApplicationStartupStatus Expected { get; set; } = new();
        public object? Query { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IActorProducer.RequestAsync)
                && args is { Length: >= 2 }
                && args[1] is GetApplicationStartupStatusQuery query)
            {
                Query = query;
                return new ValueTask<ServiceResult<ApplicationStartupStatus>>(
                    new ServiceOk<ApplicationStartupStatus>(Expected));
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}

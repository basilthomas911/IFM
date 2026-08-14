using Microsoft.Extensions.Logging;
using System.Reflection;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Application.Actor.Command.EventProjector;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationEventProjectorTests
{
    [Fact]
    public void Application_lifecycle_descriptors_are_complete_and_explicitly_non_durable()
    {
        var projector = new ApplicationEventProjector(
            Stub<IDurableReplayQueue>(),
            Stub<IEventSourceActorDbContext>(),
            Stub<IBlackboardService>(),
            Stub<ILogger<ApplicationEventProjector>>());

        Assert.Collection(
            projector.ProjectionDescriptors.OrderBy(item => item.SourceEventType.Name),
            descriptor =>
            {
                Assert.Equal(typeof(ApplicationShutdownEvent), descriptor.SourceEventType);
                Assert.False(descriptor.UseDurableReplay);
            },
            descriptor =>
            {
                Assert.Equal(typeof(ApplicationStartupEvent), descriptor.SourceEventType);
                Assert.False(descriptor.UseDurableReplay);
            });
        Assert.Equal(
            projector.ProjectionDescriptors.Select(item => item.SourceEventType).OrderBy(type => type.Name),
            projector.ProjectedEventTypes.OrderBy(type => type.Name));
    }

    static T Stub<T>() where T : class => DispatchProxy.Create<T, InterfaceStub>();

    public class InterfaceStub : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
    }
}

using Microsoft.Extensions.Logging;
using System.Reflection;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Application.Command.Actor;
using TomasAI.IFM.Domain.Application.Command.EventProjector;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationEventProjectorTests
{
    [Fact]
    public void Application_lifecycle_descriptors_are_complete_and_durable()
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
                Assert.True(descriptor.UseDurableReplay);
                Assert.False(descriptor.PublishTerminalEvent);
            },
            descriptor =>
            {
                Assert.Equal(typeof(ApplicationStartupEvent), descriptor.SourceEventType);
                Assert.True(descriptor.UseDurableReplay);
                Assert.False(descriptor.PublishTerminalEvent);
            });
        Assert.Equal(
            projector.ProjectionDescriptors.Select(item => item.SourceEventType).OrderBy(type => type.Name),
            projector.ProjectedEventTypes.OrderBy(type => type.Name));
    }

    [Fact]
    public void Concurrent_descriptor_type_reads_share_one_immutable_collection()
    {
        var projector = new ApplicationEventProjector(
            Stub<IDurableReplayQueue>(), Stub<IEventSourceActorDbContext>(),
            Stub<IBlackboardService>(), Stub<ILogger<ApplicationEventProjector>>());
        var reads = new IReadOnlyCollection<Type>[32];
        Parallel.For(0, reads.Length, index => reads[index] = projector.ProjectedEventTypes);
        Assert.All(reads, types => Assert.Same(reads[0], types));
        Assert.Equal(projector.ProjectionDescriptors.Select(item => item.SourceEventType), reads[0]);
        var collection = Assert.IsAssignableFrom<ICollection<Type>>(reads[0]);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(typeof(string)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Runtime_allowlist_controls_durable_delivery(bool includeProjector)
    {
        var projector = new ApplicationEventProjector(
            Stub<IDurableReplayQueue>(),
            Stub<IEventSourceActorDbContext>(),
            Stub<IBlackboardService>(),
            Stub<ILogger<ApplicationEventProjector>>(),
            new EventProjectorReliabilityOptions
            {
                DurableProjectorAllowlist = includeProjector
                    ? [nameof(ApplicationEventProjector)] : []
            });
        var method = typeof(BaseEventProjector<ApplicationCommandActor>)
            .GetMethod("GetDescriptorMap", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var descriptors = (IReadOnlyDictionary<Type, EventProjectionDescriptor>)method.Invoke(projector, null)!;

        Assert.All(descriptors.Values, descriptor =>
            Assert.Equal(includeProjector, descriptor.UseDurableReplay));
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

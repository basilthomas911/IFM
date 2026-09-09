using NSubstitute;
using System.Diagnostics;
using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public sealed class ActorTraceTests
{
    [Fact]
    public void W3c_context_roundtrips_without_business_payload_or_baggage()
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.TraceStateString = "vendor=value";
        activity.AddBaggage("private", "not-exported");
        var headers = ActorTrace.Headers()!;
        var restored = ActorTrace.Extract(headers);
        restored.TraceId.Should().Be(activity.TraceId);
        restored.SpanId.Should().Be(activity.SpanId);
        restored.TraceState.Should().Be("vendor=value");
        restored.IsRemote.Should().BeTrue();
        headers.ContainsKey("baggage").Should().BeFalse();
    }

    [Fact]
    public void Processing_uses_message_context_and_never_inherits_worker_startup_context()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ActorTrace.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        using var startup = new Activity("worker-startup").SetIdFormat(ActivityIdFormat.W3C).Start();
        var message = NSubstitute.Substitute.For<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorMessage>();
        message.Subject.Returns(new ActorSubject(ActorType.Command, "IntrinsicTimeStrategyWorkflowCommand", "Execute", "test"));
        using (var root = ActorTrace.Start(message))
            root!.TraceId.Should().NotBe(startup.TraceId);
        var supplied = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        message.TraceContext.Returns(supplied);
        using var child = ActorTrace.Start(message);
        child!.TraceId.Should().Be(supplied.TraceId);
        child.ParentSpanId.Should().Be(supplied.SpanId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-trace")]
    [InlineData("00-00000000000000000000000000000000-0000000000000000-01")]
    public void Invalid_transport_context_is_ignored(string value)
    {
        ActorTrace.Extract(new NatsHeaders { ["traceparent"] = value }).Should().Be(default(ActivityContext));
        ActorTrace.Extract(null).Should().Be(default(ActivityContext));
    }
}

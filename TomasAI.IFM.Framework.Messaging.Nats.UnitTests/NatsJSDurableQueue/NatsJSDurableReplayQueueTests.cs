using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests.NatsJSDurableQueue;

public sealed class NatsJSDurableReplayQueueTests
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task PrepareAsync_creates_deterministic_configuration_without_starting_workers()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);

        await queue.PrepareAsync("Fund.Projector", TimeSpan.FromSeconds(10));

        var settings = transport.Queues["Fund.Projector"].Settings;
        settings.Names.ProcessStream.Should().Be("IFM_Fund_Projector_PROCESS");
        settings.Names.ProcessSubject.Should().Be("ifm.projector.Fund_Projector.process");
        settings.Names.ProcessConsumer.Should().Be("Fund_Projector-process-worker");
        settings.Names.ReplayStream.Should().Be("IFM_Fund_Projector_REPLAY");
        settings.Names.ReplaySubject.Should().Be("ifm.projector.Fund_Projector.replay");
        settings.Names.ReplayConsumer.Should().Be("Fund_Projector-replay-worker");
        settings.MaxReplayAttempts.Should().Be(3);
        settings.Backoff.Should().Equal(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40));
        transport.Queues["Fund.Projector"].ProcessConsumerStarts.Should().Be(0);
        transport.Queues["Fund.Projector"].ReplayConsumerStarts.Should().Be(0);
    }

    [Fact]
    public async Task Enqueue_before_start_is_durable_but_not_consumed_until_explicit_start()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var calls = 0;
        await queue.PrepareAsync("projector", TimeSpan.FromSeconds(30));
        await queue.DequeueAsync("projector", _ =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });

        await queue.EnqueueAsync("projector", SampleData.Event("staged"));
        await Task.Delay(50);

        calls.Should().Be(0);
        transport.Queues["projector"].ProcessPublishCount.Should().Be(1);
        transport.Queues["projector"].ProcessConsumerStarts.Should().Be(0);
        transport.Queues["projector"].ReplayConsumerStarts.Should().Be(0);

        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));

        await EventuallyAsync(() => calls == 1);
    }

    [Fact]
    public async Task Enqueue_processes_and_acknowledges_a_successful_event()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        SampleEvent? processed = null;
        await queue.DequeueAsync("projector", domainEvent =>
        {
            processed = (SampleEvent)domainEvent;
            return Task.CompletedTask;
        });
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));

        await queue.EnqueueAsync("projector", SampleData.Event("success"));

        await EventuallyAsync(() => processed?.Value == "success");
        var state = transport.Queues["projector"];
        state.ReplayPublishCount.Should().Be(0);
        state.LastProcessMessage!.AckCount.Should().Be(1);
    }

    [Fact]
    public async Task Process_failure_is_published_to_replay_then_process_message_is_acked()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var calls = 0;
        await queue.DequeueAsync("projector", _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new InvalidOperationException("process failed");
            return Task.CompletedTask;
        });
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));

        await queue.EnqueueAsync("projector", SampleData.Event());

        await EventuallyAsync(() => calls == 2);
        var state = transport.Queues["projector"];
        state.ReplayPublishCount.Should().Be(1);
        state.LastProcessMessage!.AckCount.Should().Be(1);
        state.LastReplayMessage!.AckCount.Should().Be(1);
    }

    [Fact]
    public async Task Stream_order_deferral_naks_process_without_consuming_replay_attempts()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var calls = 0;
        await queue.DequeueAsync("projector", domainEvent =>
        {
            if (Interlocked.Increment(ref calls) < 3)
                throw new EventProjectorStreamOrderDeferredException("projector", domainEvent.EventId);
            return Task.CompletedTask;
        });
        await queue.StartAsync("projector", TimeSpan.FromMilliseconds(1));

        await queue.EnqueueAsync("projector", SampleData.Event("ordered"));

        await EventuallyAsync(() => calls == 3);
        var state = transport.Queues["projector"];
        state.ReplayPublishCount.Should().Be(0);
        state.LastProcessMessage!.NakCount.Should().Be(2);
        state.LastProcessMessage.AckCount.Should().Be(1);
    }

    [Fact]
    public async Task Replay_publish_failure_naks_process_message_and_worker_completes_the_handoff_on_redelivery()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var calls = 0;
        await queue.DequeueAsync("projector", _ =>
        {
            if (Interlocked.Increment(ref calls) <= 2)
                throw new InvalidOperationException("process failed");
            return Task.CompletedTask;
        });
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));
        var state = transport.Queues["projector"];
        state.ReplayPublishFailuresRemaining = 1;

        await queue.EnqueueAsync("projector", SampleData.Event());

        await EventuallyAsync(() => calls == 3 && state.LastReplayMessage?.AckCount == 1);
        state.ProcessConsumerStarts.Should().Be(1);
        state.ReplayPublishAttempts.Should().Be(2);
        state.ReplayPublishCount.Should().Be(1);
        state.ReplayMessageIds.Should().ContainSingle();
        state.LastProcessMessage!.DeliveryCount.Should().Be(2);
        state.LastProcessMessage.NakCount.Should().Be(1);
        state.LastProcessMessage.AckCount.Should().Be(1);
    }

    [Fact]
    public async Task Process_ack_failure_after_replay_publish_reuses_replay_message_id_on_redelivery()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        queue.SetMaxReplayAttemps("projector", 1);
        var terminalCalls = 0;
        queue.SetMaxAttemptsReachedAction("projector", _ =>
        {
            Interlocked.Increment(ref terminalCalls);
            return Task.CompletedTask;
        });
        await queue.DequeueAsync("projector", _ => throw new InvalidOperationException("projection failed"));
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));
        var state = transport.Queues["projector"];
        state.ProcessAckFailuresRemaining = 1;

        await queue.EnqueueAsync("projector", SampleData.Event());

        await EventuallyAsync(() => terminalCalls == 1
            && state.LastProcessMessage?.AckCount == 1
            && state.LastReplayMessage?.AckCount == 1);
        state.LastProcessMessage!.AckAttempts.Should().Be(2);
        state.LastProcessMessage.NakCount.Should().Be(1);
        state.LastProcessMessage.DeliveryCount.Should().Be(2);
        state.ReplayPublishAttempts.Should().Be(2);
        state.ReplayPublishCount.Should().Be(1);
        state.ReplayMessageIds.Should().ContainSingle();
    }

    [Fact]
    public async Task Repeated_enqueue_of_same_event_uses_one_process_message_id()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var calls = 0;
        await queue.DequeueAsync("projector", _ =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));
        var domainEvent = SampleData.Event();

        await queue.EnqueueAsync("projector", domainEvent);
        await queue.EnqueueAsync("projector", domainEvent);

        await EventuallyAsync(() => calls == 1);
        await Task.Delay(50);
        var state = transport.Queues["projector"];
        calls.Should().Be(1);
        state.ProcessPublishAttempts.Should().Be(2);
        state.ProcessPublishCount.Should().Be(1);
        state.ProcessMessageIds.Should().ContainSingle();
        state.LastProcessMessageId.Should().Be($"projector:process:event-{domainEvent.EventId}");
    }

    [Fact]
    public async Task Replay_failure_is_nakd_until_a_later_attempt_succeeds()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var calls = 0;
        await queue.DequeueAsync("projector", _ =>
        {
            if (Interlocked.Increment(ref calls) < 3)
                throw new InvalidOperationException("retry");
            return Task.CompletedTask;
        });
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));

        await queue.EnqueueAsync("projector", SampleData.Event());

        await EventuallyAsync(() => calls == 3);
        var state = transport.Queues["projector"];
        state.ReplayPublishCount.Should().Be(1);
        state.LastReplayMessage!.NakCount.Should().Be(1);
        state.LastReplayMessage.AckCount.Should().Be(1);
    }

    [Fact]
    public async Task Replay_at_max_delivery_invokes_terminal_action_and_acknowledges()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        queue.SetMaxReplayAttemps("projector", 2);
        var terminalCalls = 0;
        queue.SetMaxAttemptsReachedAction("projector", _ =>
        {
            Interlocked.Increment(ref terminalCalls);
            return Task.CompletedTask;
        });
        await queue.DequeueAsync("projector", _ => throw new InvalidOperationException("always fails"));
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));

        await queue.EnqueueAsync("projector", SampleData.Event());

        await EventuallyAsync(() => terminalCalls == 1
            && transport.Queues["projector"].LastReplayMessage?.AckCount == 1);
        await Task.Delay(50);
        terminalCalls.Should().Be(1);
        transport.Queues["projector"].LastReplayMessage!.NakCount.Should().Be(1);
    }

    [Fact]
    public async Task Projector_state_and_handlers_are_isolated_by_projector_name()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var first = new List<string>();
        var second = new List<string>();
        await queue.DequeueAsync("first", e => { first.Add(((SampleEvent)e).Value); return Task.CompletedTask; });
        await queue.DequeueAsync("second", e => { second.Add(((SampleEvent)e).Value); return Task.CompletedTask; });
        await queue.StartAsync("first", TimeSpan.FromSeconds(30));
        await queue.StartAsync("second", TimeSpan.FromSeconds(30));
        queue.SetMaxReplayAttemps("first", 2);
        queue.SetMaxReplayAttemps("second", 5);

        await queue.EnqueueAsync("first", SampleData.Event("one"));
        await queue.EnqueueAsync("second", SampleData.Event("two"));

        await EventuallyAsync(() => first.Count == 1 && second.Count == 1);
        first.Should().Equal("one");
        second.Should().Equal("two");
        queue.GetMaxReplayAttemps("first").Should().Be(2);
        queue.GetMaxReplayAttemps("second").Should().Be(5);
    }

    [Fact]
    public async Task Configuration_respects_overwrite_false()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        queue.SetMaxReplayAttemps("projector", 4, overwrite: false);
        queue.SetMaxReplayAttemps("projector", 8, overwrite: false);

        queue.GetMaxReplayAttemps("projector").Should().Be(4);
    }

    [Fact]
    public async Task Enqueue_restarts_both_workers_after_idle_timeout()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport, TimeSpan.FromMilliseconds(80));
        var calls = 0;
        await queue.DequeueAsync("projector", _ => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));
        await EventuallyAsync(() => transport.Queues["projector"].ProcessConsumerStarts == 1);
        await Task.Delay(180);

        await queue.EnqueueAsync("projector", SampleData.Event());

        await EventuallyAsync(() => calls == 1);
        transport.Queues["projector"].ProcessConsumerStarts.Should().BeGreaterThan(1);
        transport.Queues["projector"].ReplayConsumerStarts.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task StopAsync_disables_consumption_until_explicit_restart()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);
        var calls = 0;
        await queue.DequeueAsync("projector", _ => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));

        await queue.StopAsync("projector");
        await queue.EnqueueAsync("projector", SampleData.Event());

        await Task.Delay(50);
        calls.Should().Be(0);
        transport.Queues["projector"].ProcessConsumerStarts.Should().Be(1);
        await queue.StartAsync("projector", TimeSpan.FromSeconds(30));

        await EventuallyAsync(() => calls == 1);
        transport.Queues["projector"].ProcessConsumerStarts.Should().Be(2);
        transport.Queues["projector"].ReplayConsumerStarts.Should().Be(2);
    }

    [Fact]
    public async Task StopAsync_after_queue_disposal_is_an_idempotent_no_op()
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        var queue = CreateQueue(transport);
        await queue.DequeueAsync("projector", _ => Task.CompletedTask);
        await queue.DisposeAsync();

        Func<Task> stop = () => queue.StopAsync("projector");

        await stop.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SetMaxReplayAttempts_rejects_non_positive_values(int value)
    {
        var transport = new FakeNatsJSDurableQueueTransport();
        await using var queue = CreateQueue(transport);

        var action = () => queue.SetMaxReplayAttemps("projector", value);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    static NatsJSDurableReplayQueue CreateQueue(
        FakeNatsJSDurableQueueTransport transport,
        TimeSpan? idleTimeout = null) =>
        new(transport, idleTimeout ?? TimeSpan.FromMinutes(2));

    static async Task EventuallyAsync(Func<bool> condition)
    {
        var expires = DateTime.UtcNow + TestTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= expires)
                throw new TimeoutException("The expected asynchronous condition was not reached.");
            await Task.Delay(10);
        }
    }
}

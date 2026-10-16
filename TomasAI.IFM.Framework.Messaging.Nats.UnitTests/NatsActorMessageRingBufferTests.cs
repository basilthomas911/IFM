using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsActorMessageRingBufferTests
{
    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void Enqueue_dequeue_preserves_count_across_many_physical_wraps()
    {
        using var buffer = new NatsActorMessageRingBuffer(8);
        var message = default(NatsMsg<byte[]>);

        for (var cycle = 0; cycle < 10_000; cycle++)
        {
            buffer.TryEnqueue(message, CancellationToken.None);
            buffer.Count.Should().Be(1);
            buffer.TryDequeue(out _, CancellationToken.None);
            buffer.Count.Should().Be(0);
        }
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void Enqueue_blocks_at_exact_capacity_and_honors_cancellation()
    {
        using var buffer = new NatsActorMessageRingBuffer(8);
        var message = default(NatsMsg<byte[]>);
        for (var i = 0; i < buffer.Capacity; i++)
            buffer.TryEnqueue(message, CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = () => buffer.TryEnqueue(message, cancellation.Token);

        action.Should().Throw<OperationCanceledException>();
        buffer.Count.Should().Be(buffer.Capacity);
    }
}

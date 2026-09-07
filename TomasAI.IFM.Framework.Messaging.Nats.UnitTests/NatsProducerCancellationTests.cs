using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsProducerCancellationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RequestScope_PropagatesCallerOrShutdownCancellation(bool supplyCaller, bool cancelCaller)
    {
        using var stopping = new CancellationTokenSource();
        using var caller = new CancellationTokenSource();
        var producer = CreateProducer(stopping);
        var scope = CreateScope(producer, supplyCaller ? caller.Token : CancellationToken.None);
        using var owned = (IDisposable)scope;
        var token = ReadToken(scope);
        token.CanBeCanceled.Should().BeTrue();
        token.IsCancellationRequested.Should().BeFalse();
        if (cancelCaller) caller.Cancel(); else stopping.Cancel();
        token.IsCancellationRequested.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposingRequestScope_PreservesProducerAndCallerTokenSources(bool supplyCaller)
    {
        using var stopping = new CancellationTokenSource();
        using var caller = new CancellationTokenSource();
        var producer = CreateProducer(stopping);
        ((IDisposable)CreateScope(producer, supplyCaller ? caller.Token : CancellationToken.None)).Dispose();
        var next = CreateScope(producer, CancellationToken.None);
        using var owned = (IDisposable)next;
        caller.Cancel();
        ReadToken(next).IsCancellationRequested.Should().BeFalse();
        stopping.Cancel();
        ReadToken(next).IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void AlreadyCanceledCaller_RemainsCanceled()
    {
        using var stopping = new CancellationTokenSource();
        var scope = CreateScope(CreateProducer(stopping), new CancellationToken(true));
        using var owned = (IDisposable)scope;
        ReadToken(scope).IsCancellationRequested.Should().BeTrue();
    }

    static NatsActorProducer CreateProducer(CancellationTokenSource stopping)
    {
        var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance);
        typeof(NatsActorProducer).GetField("_operationStopping", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(producer, stopping);
        return producer;
    }

    static object CreateScope(NatsActorProducer producer, CancellationToken token) => typeof(NatsActorProducer)
        .GetMethod("CreateOperationCancellation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(producer, [token])!;
    static CancellationToken ReadToken(object scope) => (CancellationToken)scope.GetType().GetProperty("Token")!.GetValue(scope)!;
}

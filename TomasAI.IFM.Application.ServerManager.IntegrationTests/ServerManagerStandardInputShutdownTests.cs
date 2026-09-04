using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

public sealed class ServerManagerStandardInputShutdownTests
{
    [Fact]
    public async Task End_of_input_requests_graceful_host_shutdown()
    {
        using var lifetime = new TestHostApplicationLifetime();

        await ServerManagerStandardInputShutdown.MonitorAsync(
            new StringReader(string.Empty),
            lifetime,
            NullLogger.Instance);

        lifetime.ApplicationStopping.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Exact_shutdown_message_requests_graceful_host_shutdown()
    {
        using var lifetime = new TestHostApplicationLifetime();

        await ServerManagerStandardInputShutdown.MonitorAsync(
            new StringReader("shutdown" + Environment.NewLine),
            lifetime,
            NullLogger.Instance);

        lifetime.ApplicationStopping.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Invalid_message_is_ignored_until_shutdown_message_arrives()
    {
        using var lifetime = new TestHostApplicationLifetime();

        await ServerManagerStandardInputShutdown.MonitorAsync(
            new StringReader("not-shutdown" + Environment.NewLine + "shutdown" + Environment.NewLine),
            lifetime,
            NullLogger.Instance);

        lifetime.ApplicationStopping.IsCancellationRequested.Should().BeTrue();
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication() => _stopping.Cancel();

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}

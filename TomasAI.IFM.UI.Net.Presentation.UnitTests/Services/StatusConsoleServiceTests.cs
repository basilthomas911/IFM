using NSubstitute;
using TomasAI.IFM.Service.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.Net.Services.Application;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

public sealed class StatusConsoleServiceTests
{
    [Fact]
    public async Task Write_failures_are_returned_to_the_awaiting_error_boundary()
    {
        var writer = Substitute.For<IStatusConsoleWriter>();
        var failure = new IOException("Status console unavailable");
        writer.WriteConsoleAsync(LogSourceType.IFMApp, "message").Returns(Task.FromException(failure));
        writer.WriteConsoleAsync(LogSourceType.IFMApp, 503, "message").Returns(Task.FromException(failure));
        var service = new StatusConsoleService(writer, Substitute.For<IStatusConsoleEventConsumer>());
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.WriteConsoleAsync(LogSourceType.IFMApp, "message")));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.WriteConsoleAsync(LogSourceType.IFMApp, 503, "message")));
    }

    [Fact]
    public async Task Listener_start_and_stop_failures_are_not_reported_as_success()
    {
        var consumer = Substitute.For<IStatusConsoleEventConsumer>();
        var failure = new IOException("Listener unavailable");
        consumer.StartAsync(Arg.Any<Func<StatusConsoleLoggedEvent, ValueTask>>()).Returns(ValueTask.FromException(failure));
        consumer.StopAsync().Returns(ValueTask.FromException(failure));
        var service = new StatusConsoleService(Substitute.For<IStatusConsoleWriter>(), consumer);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.StartStatusConsoleLogListenerAsync(_ => ValueTask.CompletedTask, Guid.NewGuid())));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.StopStatusConsoleLogListener(Guid.NewGuid())));
    }
}

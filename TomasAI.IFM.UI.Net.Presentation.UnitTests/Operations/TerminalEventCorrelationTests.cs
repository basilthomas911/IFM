using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Operations;

public class TerminalEventCorrelationTests
{
    [Fact]
    public async Task EventBeforeCommandResponse_IsBufferedAndMatchedByExactCommandId()
    {
        var commandId = Guid.NewGuid();
        var correlation = new TerminalEventCorrelation();
        var terminalEvent = new YieldCurveRatesImportedCompleteEvent { CommandId = commandId };
        correlation.BeginAttempt();

        correlation.TryPublish(terminalEvent).Should().BeTrue();
        var observed = await correlation.AwaitAsync(commandId, CancellationToken.None);

        observed.Should().BeSameAs(terminalEvent);
        correlation.CommandId.Should().Be(commandId);
        correlation.EndAttempt();
        correlation.CommandId.Should().BeEmpty();
    }

    [Fact]
    public async Task UnrelatedEvent_IsIgnoredAfterCommandIdIsKnown()
    {
        var commandId = Guid.NewGuid();
        var correlation = new TerminalEventCorrelation();
        correlation.BeginAttempt();
        var observation = correlation.AwaitAsync(commandId, CancellationToken.None);

        correlation.TryPublish(new YieldCurveRatesImportedCompleteEvent
        {
            CommandId = Guid.NewGuid()
        }).Should().BeFalse();
        observation.IsCompleted.Should().BeFalse();

        var expected = new YieldCurveRatesImportedCompleteEvent { CommandId = commandId };
        correlation.TryPublish(expected).Should().BeTrue();
        (await observation).Should().BeSameAs(expected);
        correlation.EndAttempt();
    }

    [Fact]
    public async Task BoundedObservation_UsesProvidedTimeProvider()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));
        var correlation = new TerminalEventCorrelation();
        correlation.BeginAttempt();
        var observation = correlation.AwaitAsync(
            Guid.NewGuid(),
            TimeSpan.FromSeconds(30),
            timeProvider,
            CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromSeconds(29));
        observation.IsCompleted.Should().BeFalse();
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        await FluentActions.Awaiting(() => observation).Should().ThrowAsync<TimeoutException>();
        correlation.EndAttempt();
    }

    [Fact]
    public async Task EndAttempt_CancelsPendingObservationAndAllowsAnotherAttempt()
    {
        var correlation = new TerminalEventCorrelation();
        correlation.BeginAttempt();
        var first = correlation.AwaitAsync(Guid.NewGuid(), CancellationToken.None);

        correlation.EndAttempt();

        await FluentActions.Awaiting(() => first).Should().ThrowAsync<OperationCanceledException>();
        correlation.BeginAttempt();
        correlation.EndAttempt();
    }
}

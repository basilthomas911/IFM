using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesRsiSignal;

/// <summary>Specifies replayable daily RSI collection generation.</summary>
public sealed class FuturesRsiDailyCollectionFeatureTests
{
    [Fact]
    public void GivenValidDailyHistory_WhenAnotherDailyPriceArrives_ThenSignalAndCollectionReplayTogether()
    {
        var date = new DateOnly(2026, 9, 15);
        var state = new FuturesRsiSignalCommandState();
        for (var day = 0; day < 14; day++)
            state.Apply(new FuturesRsiDailySignalGeneratedEvent
            {
                FuturesRsiSignal = new FuturesRsiSignalReadModel
                {
                    ContractId = "ESU6", ValueDate = date.AddDays(-14 + day),
                    TimePeriod = TimeFrameType.Daily, PeriodLength = 14,
                    Price = 6500m + day, RSI = 55d
                }
            }, addEvent: false).Should().BeTrue();

        var signalId = new FuturesRsiSignalId("ESU6", date, TimeFrameType.Daily,
            14, new TimeOnly(16, 0));
        var command = new GenerateFuturesRsiDailySignalCommand(signalId, 6515m)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command,
                GenerateFuturesRsiDailySignalCommand.Actor,
                GenerateFuturesRsiDailySignalCommand.Verb, signalId.ToEntityDailyId().Format())
        };

        command.Execute(state).Success.Should().BeTrue();
        state.Events.Should().HaveCount(2);
        state.Events.Last().Should().BeOfType<FuturesRsiDailySignalsGeneratedEvent>();

        var restored = new FuturesRsiSignalCommandState();
        restored.ReplayEvents(state.Events.ToArray());
        restored.Events.Should().BeEmpty();
    }
}

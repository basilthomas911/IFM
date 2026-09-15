using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

/// <summary>Checks that a repeated closed observation does not create another derived signal event.</summary>
public sealed class ClosedObservationNoOpGuardTests
{
    [Fact]
    public void AdxRepeatedObservationAcknowledgesWithoutAppendingAnEvent()
    {
        var observation = SampleData.AtrObservation;
        var previous = SampleData.CreateAdxSignalGeneratedEvent();
        var state = new FuturesAdxSignalCommandState();
        Assert.True(state.Apply(previous with
        {
            FuturesAdxSignal = previous.FuturesAdxSignal with
            {
                Metadata = new MarketAnalyticsSignalMetadata
                {
                    ObservationId = observation.ObservationId
                }
            }
        }, addEvent: false));
        var command = new GenerateFuturesAdxSignalCommand(
            SampleData.AdxSignalId with { TimePeriod = TimeFrameType.FifteenSeconds },
            (decimal)SampleData.FuturesPrice) with { Observation = observation };

        Assert.True(command.Execute(state).Success);
        Assert.Empty(state.Events);
    }

    [Fact]
    public void MacdRepeatedObservationAcknowledgesWithoutAppendingAnEvent()
    {
        var observation = SampleData.AtrObservation;
        var previous = SampleData.CreateMacdSignalGeneratedEvent();
        var state = new FuturesMacdSignalCommandState();
        Assert.True(state.Apply(previous with
        {
            FuturesMacdSignal = previous.FuturesMacdSignal with
            {
                Metadata = new MarketAnalyticsSignalMetadata
                {
                    ObservationId = observation.ObservationId
                }
            }
        }, addEvent: false));
        var command = new GenerateFuturesMacdSignalCommand(
            SampleData.MacdSignalId with { TimePeriod = TimeFrameType.FifteenSeconds },
            (decimal)SampleData.FuturesPrice) with { Observation = observation };

        Assert.True(command.Execute(state).Success);
        Assert.Empty(state.Events);
    }
}

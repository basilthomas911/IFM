using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAdxSignal;

public sealed class FuturesAdxCommandConventionTests
{
    [Fact]
    public void Insufficient_seed_leaves_existing_history_and_pending_events_unchanged()
    {
        var state = new FuturesAdxSignalCommandState();
        var generated = SampleData.AdxGenerateCommand with { CommandId = Guid.NewGuid() };
        Assert.True(generated.Execute(state).Success);
        var previous = state.AdxSignal;
        var pending = state.Events.ToArray();
        var command = new StartFuturesAdxSignalCommand(SampleData.AdxEntityId)
        {
            CommandId = Guid.NewGuid(), HistoricalSeed = Bars(1)
        };
        Assert.False(command.Execute(state).Success);
        Assert.Same(previous, state.AdxSignal);
        Assert.Equal(pending, state.Events.ToArray());
    }

    [Fact]
    public void Accepted_seed_preserves_start_command_identity_and_rehydrates_warm_history()
    {
        var state = new FuturesAdxSignalCommandState();
        var command = new StartFuturesAdxSignalCommand(SampleData.AdxEntityId)
        {
            CommandId = Guid.NewGuid(), HistoricalSeed = Bars(SampleData.AdxEntityId.PeriodLength + 2)
        };
        Assert.True(command.Execute(state).Success);
        Assert.True(state.AdxSignal.IsWarm);
        Assert.All(state.Events, e => Assert.Equal(command.CommandId, e.CommandId));
        var restored = new FuturesAdxSignalCommandState();
        foreach (var e in state.Events)
        {
            if (e is FuturesAdxSignalStartedEvent started)
                restored.Apply(MessagePackSerializer.Deserialize<FuturesAdxSignalStartedEvent>(MessagePackSerializer.Serialize(started)), false);
            else if (e is FuturesAdxSignalGeneratedEvent generated)
                restored.Apply(MessagePackSerializer.Deserialize<FuturesAdxSignalGeneratedEvent>(MessagePackSerializer.Serialize(generated)), false);
        }
        Assert.Equal(state.AdxSignals.Count, restored.AdxSignals.Count);
        Assert.True(restored.AdxSignal.IsWarm);
        Assert.Equal(state.AdxSignal.AdxValue, restored.AdxSignal.AdxValue);
    }

    [Fact]
    public void Stop_event_keeps_originating_command_identity()
    {
        var state = new FuturesAdxSignalCommandState();
        var command = new StopFuturesAdxSignalCommand(SampleData.AdxEntityId) { CommandId = Guid.NewGuid() };
        Assert.True(command.Execute(state).Success);
        Assert.Equal(command.CommandId, Assert.Single(state.Events.OfType<FuturesAdxSignalStoppedEvent>()).CommandId);
    }

    static FuturesTradeSessionBarReadModel[] Bars(int count) => Enumerable.Range(0, count)
        .Select(i => new FuturesTradeSessionBarReadModel
        {
            Close = 100 + i, LastMarketEventUtc = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero).AddMinutes(i * 5),
            ContractId = SampleData.AdxEntityId.ContractId, ValueDate = SampleData.AdxEntityId.ValueDate
        }).ToArray();
}

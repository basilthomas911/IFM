using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesVwapSignal;

/// <summary>Checks expected recovery ordering failures are results rather than exceptions.</summary>
public sealed class FuturesVwapRecoveryGuardTests
{
    [Fact]
    public void NoninitialRecoveryBatchWithoutCheckpointFailsWithoutAWrite()
    {
        var entityId = new FuturesVwapSignalEntityId("ESU6",
            new DateOnly(2026, 9, 15), FuturesVwapConfiguration.Standard.ConfigurationId);
        var command = new RecoverFuturesVwapSignalCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command,
                UpdateFuturesVwapSignalCommand.Actor,
                RecoverFuturesVwapSignalCommand.Verb, entityId.Format()),
            EntityId = entityId,
            RecoveryGenerationId = Guid.NewGuid(), BatchOrdinal = 1,
            IsFirstBatch = false, Trades = []
        };
        var state = new FuturesVwapSignalCommandState();

        var result = command.Execute(state);

        Assert.False(result.Success);
        Assert.Empty(state.Events);
        Assert.Null(state.Checkpoint);
    }
}

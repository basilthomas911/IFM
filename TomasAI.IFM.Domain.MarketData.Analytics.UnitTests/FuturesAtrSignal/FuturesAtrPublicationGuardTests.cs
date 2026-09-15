using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesAtrSignal;

/// <summary>Checks ATR identity guards before durable state mutation.</summary>
public sealed class FuturesAtrPublicationGuardTests
{
    [Fact]
    public void MismatchedSourceContractReturnsFailureWithoutThrowingOrApplyingAnEvent()
    {
        var command = SampleData.AtrGenerateCommand with
        {
            Observation = SampleData.AtrObservation with { ContractId = "OTHER" }
        };
        var state = new FuturesAtrSignalCommandState();

        var result = command.Execute(state);

        Assert.False(result.Success);
        Assert.Empty(state.Events);
        Assert.Null(state.CalculationState);
    }
}

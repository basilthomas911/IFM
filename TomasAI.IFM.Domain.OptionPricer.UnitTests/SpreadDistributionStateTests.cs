using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests;

public class SpreadDistributionStateTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceptedProjectionEventsMarkStateUpdated(bool insert)
    {
        var state = new SpreadDistributionCommandState();
        IEvent e = insert ? new SpreadDistributionInsertedEvent() : new SpreadDistributionDeletedEvent();
        Assert.True(state.Update(e));
        Assert.True(state.Updated);
        Assert.Same(e, Assert.Single(state.Events));
        state.AcceptChanges();
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
        state.ReplayEvents(new[] { e }.AsEnumerable());
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
    }
}

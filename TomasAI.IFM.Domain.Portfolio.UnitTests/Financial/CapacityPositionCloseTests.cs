using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

[Trait("Category","PortfolioFinancial")]
public sealed class CapacityPositionCloseTests
{
    [Theory]
    [InlineData(4, .6, ReservationStatus.Filled)]
    [InlineData(10, 0, ReservationStatus.Released)]
    public void Reconciled_close_reduces_only_open_position_units(int closed,double fraction,ReservationStatus status)
    {
        var state=Filled(); var change=Close(state,closed);
        var next=CapacityLifecycleModel.Apply(state,change,false,DateTime.UtcNow);
        next.Status.Should().Be(status); next.FilledUnits.Should().Be(10); next.ClosedUnits.Should().Be(closed);
        next.PositionFraction.Should().Be((decimal)fraction); next.WorkingFraction.Should().Be(0);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(11)]
    public void Close_requires_a_positive_bounded_new_quantity(int closed)
    {
        var state=Filled(); Action act=()=>CapacityLifecycleModel.Apply(state,Close(state,closed),false,DateTime.UtcNow);
        act.Should().Throw<FinancialOperationException>();
    }
    [Fact]
    public void Ordinary_lifecycle_command_cannot_smuggle_position_release()
    {
        var state=Filled() with { Status=ReservationStatus.PartiallyFilled,FilledUnits=4,RemainingUnits=6 };
        var change=Close(state,4) with { ChangeKind=CapacityChangeKind.RequestCancel };
        Action act=()=>CapacityLifecycleModel.Apply(state,change,false,DateTime.UtcNow);
        act.Should().Throw<FinancialOperationException>();
    }
    [Fact]
    public void Closed_partial_fill_keeps_unfilled_working_obligation()
    {
        var state=Filled() with { Status=ReservationStatus.PartiallyFilled,FilledUnits=4,RemainingUnits=6 };
        var next=CapacityLifecycleModel.Apply(state,Close(state,4),false,DateTime.UtcNow);
        next.Status.Should().Be(ReservationStatus.PartiallyFilled); next.WorkingFraction.Should().Be(.6m);
        next.PositionFraction.Should().Be(0); next.FilledUnits.Should().Be(4);
    }
    static ReservationSnapshot Filled()=>new(Guid.NewGuid(),3,ReservationStatus.Filled,10,10,0,0,new('A',64),DateTime.UtcNow.AddMinutes(1),Guid.NewGuid(),1);
    static CapacityLifecycleRequest Close(ReservationSnapshot state,int closed)=>new()
    {
        ReservationId=state.ReservationId,ExpectedReservationVersion=state.Version,ChangeKind=CapacityChangeKind.RecordPositionClose,
        ExecutionId=state.ExecutionId!.Value,ExecutionRevision=2,ExpectedRequirementsHash=state.RequirementsHash,
        FilledUnits=state.FilledUnits,CancelledUnits=state.CancelledUnits,RemainingUnits=state.RemainingUnits,ClosedUnits=closed,
        RelatedPostingReference="committed-reconciliation/2"
    };
}

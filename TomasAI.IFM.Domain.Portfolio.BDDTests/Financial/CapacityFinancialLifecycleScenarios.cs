using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Financial;

[Trait("Category","PortfolioFinancial")]
public sealed class CapacityFinancialLifecycleScenarios
{
    static readonly DateTime Now=new(2026,9,9,0,0,0,DateTimeKind.Utc);
    [Fact]
    public void Given_two_filled_units_when_the_other_eight_are_reconciled_cancelled_then_filled_exposure_is_retained()
    {
        var current=Current(ReservationStatus.PartiallyFilled,2,8);
        var change=Change(current,CapacityChangeKind.ConfirmCancel) with { FilledUnits=2,CancelledUnits=8,RemainingUnits=0,RelatedPostingReference="execution-fact-fixture:cancel:2" };
        var result=CapacityLifecycleModel.Apply(current,change,false,Now);
        result.Status.Should().Be(ReservationStatus.Filled);result.PositionFraction.Should().Be(.2m);
        result.WorkingFraction.Should().Be(0);result.HeldFraction.Should().Be(0);
    }
    [Theory]
    [InlineData(CapacityChangeKind.MarkSubmissionUnknown,ReservationStatus.SubmissionUnknown)]
    [InlineData(CapacityChangeKind.RequestCancel,ReservationStatus.CancelPending)]
    public void Given_consumed_capacity_when_execution_is_uncertain_then_all_working_capacity_remains(
        CapacityChangeKind kind,ReservationStatus status)
    {
        var current=Current(ReservationStatus.Consumed,0,10);
        var result=CapacityLifecycleModel.Apply(current,Change(current,kind),false,Now);
        result.Status.Should().Be(status);result.WorkingFraction.Should().Be(1);result.HeldFraction.Should().Be(0);
    }
    [Fact]
    public void Given_an_expired_consumed_hold_when_expiry_is_requested_then_it_cannot_be_released()
    {
        var current=Current(ReservationStatus.Consumed,0,10) with { ValidUntilUtc=Now.AddSeconds(-1) };
        var change=Change(current,CapacityChangeKind.ExpireUnconsumed) with { CancelledUnits=10,RemainingUnits=0 };
        Action release=()=>CapacityLifecycleModel.Apply(current,change,false,Now);
        release.Should().Throw<FinancialOperationException>();
    }
    [Fact]
    public void Given_a_reconciled_closed_position_when_all_units_are_closed_then_position_capacity_is_released()
    {
        var current=Current(ReservationStatus.Filled,10,0);
        var change=Change(current,CapacityChangeKind.RecordPositionClose) with { ClosedUnits=10,RelatedPostingReference="execution-fact-fixture:close:3" };
        var result=CapacityLifecycleModel.Apply(current,change,false,Now);
        result.Status.Should().Be(ReservationStatus.Released);result.PositionFraction.Should().Be(0);
        result.FilledUnits.Should().Be(10);result.ClosedUnits.Should().Be(10);
    }
    [Fact]
    public void Given_a_reserved_order_when_a_lifecycle_command_tries_to_consume_then_only_the_function_may_authorize_it()
    {
        var current=Current(ReservationStatus.Reserved,0,10) with { ExecutionId=null };
        var change=Change(current,CapacityChangeKind.Consume) with { ExecutionId=Guid.NewGuid() };
        Action bypass=()=>CapacityLifecycleModel.Apply(current,change,false,Now);
        bypass.Should().Throw<FinancialOperationException>();
    }
    static ReservationSnapshot Current(ReservationStatus status,int filled,int remaining)
        =>new(Guid.NewGuid(),2,status,10,filled,0,remaining,new('A',64),Now.AddMinutes(1),Guid.NewGuid(),1);
    static CapacityLifecycleRequest Change(ReservationSnapshot current,CapacityChangeKind kind)=>new()
    {
        ReservationId=current.ReservationId,ExpectedReservationVersion=current.Version,ExpectedRequirementsHash=current.RequirementsHash,
        ExecutionId=current.ExecutionId??Guid.Empty,ExecutionRevision=current.ExecutionRevision,ChangeKind=kind,
        FilledUnits=current.FilledUnits,CancelledUnits=current.CancelledUnits,RemainingUnits=current.RemainingUnits,ClosedUnits=current.ClosedUnits
    };
}

using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;
[Trait("Category","PortfolioFinancial")]
public sealed class CapacityExpiryTests
{
    [Fact]
    public void Maintenance_uses_the_lifecycle_command_and_keeps_the_business_source_stable_across_attempts()
    {
        var now=DateTime.UtcNow;var candidate=new CapacityExpiryCandidate(1,2,Guid.NewGuid(),1,10,new('A',64),3,now.AddMinutes(-1));
        var first=CapacityExpiryModel.Create(candidate,now);var retry=CapacityExpiryModel.Create(candidate with { FinancialRevision=4 },now.AddSeconds(20));
        new List<ValidationError>().ValidateFinancialRequest<ChangeCapacityReservationCommand,CapacityLifecycleRequest>(first,
            ActorType.Command,ChangeCapacityReservationCommand.Actor,ChangeCapacityReservationCommand.Verb).Should().BeEmpty();
        first.Body.ChangeKind.Should().Be(CapacityChangeKind.ExpireUnconsumed);first.Body.ExecutionId.Should().BeEmpty();
        first.Body.CancelledUnits.Should().Be(10);first.Body.FilledUnits.Should().Be(0);first.Body.RemainingUnits.Should().Be(0);
        retry.OperationId.Should().NotBe(first.OperationId);retry.Body.Source.Should().Be(first.Body.Source);
        first.ExpiresAtUtc.Should().Be(now.AddSeconds(15));
    }
    [Fact]
    public void Future_capacity_cannot_be_prepared_for_expiry()
    {
        var now=DateTime.UtcNow;Action action=()=>CapacityExpiryModel.Create(new(1,2,Guid.NewGuid(),1,10,new('A',64),3,now.AddSeconds(1)),now);
        action.Should().Throw<ArgumentException>();
    }
}

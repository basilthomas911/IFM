using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;

/// <summary>Creates one persisted expiry attempt. It cannot consume or release an execution obligation.</summary>
public static class CapacityExpiryModel
{
    public static ChangeCapacityReservationCommand Create(CapacityExpiryCandidate candidate,DateTime now)
    {
        if(now.Kind!=DateTimeKind.Utc || candidate.ExpiredAtUtc>now || candidate.Units<=0 || candidate.ReservationVersion<=0)
            throw new ArgumentException("Only expired unconsumed capacity is eligible for maintenance.");
        var operation=Guid.NewGuid();var id=new CapacityReservationEntityId(candidate.PortfolioId,candidate.ReservationId);
        var source=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"CapacityExpiry:{candidate.ReservationId:N}:{candidate.ReservationVersion}")).AsSpan(0,16));
        var request=new ChangeCapacityReservationCommand
        {
            CommandId=operation,OperationId=operation,PortfolioId=candidate.PortfolioId,EntityId=id,
            Subject=new(ActorType.Command,ChangeCapacityReservationCommand.Actor,ChangeCapacityReservationCommand.Verb,id.Format()),
            CorrelationId=source,CausationId=source,RequestedAtUtc=now,ExpiresAtUtc=now.AddSeconds(15),ExpectedFinancialRevision=candidate.FinancialRevision,
            Access=new("PortfolioCapacityExpiry",["CapacityLifecycle"],[candidate.PortfolioId]),
            Body=new() { ReservationId=candidate.ReservationId,ExpectedReservationVersion=candidate.ReservationVersion,
                ExpectedRequirementsHash=candidate.RequirementsHash,ChangeKind=CapacityChangeKind.ExpireUnconsumed,
                CancelledUnits=candidate.Units,FilledUnits=0,RemainingUnits=0,ClosedUnits=0,
                Source=new() { System="PortfolioCapacityExpiry",SourceEntityId=candidate.ReservationId.ToString("N"),SourceEventId=source,
                    SourceSequence=candidate.ReservationVersion,OccurredAtUtc=candidate.ExpiredAtUtc,
                    SourceContentHash=FinancialCanonicalHash.Compute(new { candidate.ReservationId,candidate.ReservationVersion,candidate.Units,candidate.RequirementsHash,candidate.ExpiredAtUtc }) } }
        };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
}

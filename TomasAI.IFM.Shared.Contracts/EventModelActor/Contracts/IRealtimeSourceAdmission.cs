namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Fences supervised realtime generations at the actor execution boundary.</summary>
public interface IRealtimeSourceAdmission
{
    bool TryEnter(ActorSubject sourceSubject, string? dataset, Guid generationId,
        out IDisposable? processingLease);
}

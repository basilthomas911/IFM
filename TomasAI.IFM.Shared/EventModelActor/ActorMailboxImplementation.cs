namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Selects the interchangeable storage implementation used by entity actor mailboxes.</summary>
public enum ActorMailboxImplementation
{
    Channel = 0,
    MpscRing = 1,
    SpscRing = 2
}

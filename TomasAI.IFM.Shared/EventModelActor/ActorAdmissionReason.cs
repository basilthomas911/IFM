namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Bounded reasons for an actor admission decision.</summary>
public enum ActorAdmissionReason
{
    None,
    GlobalMessageLimit,
    GlobalByteLimit,
    ActorTypeMessageLimit,
    ActorTypeByteLimit,
    MailboxLimit,
    PayloadTooLarge,
    Stopping,
    MailboxRetired
}

namespace TomasAI.IFM.Shared.EventModelActor;

public static class ActorAdmissionReasonExtensions
{
    public static string ToStringFast(this ActorAdmissionReason value) => value switch
    {
        ActorAdmissionReason.None => "none",
        ActorAdmissionReason.GlobalMessageLimit => "global_message_limit",
        ActorAdmissionReason.GlobalByteLimit => "global_byte_limit",
        ActorAdmissionReason.ActorTypeMessageLimit => "actor_type_message_limit",
        ActorAdmissionReason.ActorTypeByteLimit => "actor_type_byte_limit",
        ActorAdmissionReason.MailboxLimit => "mailbox_limit",
        ActorAdmissionReason.PayloadTooLarge => "payload_too_large",
        ActorAdmissionReason.Stopping => "stopping",
        ActorAdmissionReason.MailboxRetired => "mailbox_retired",
        _ => "unknown"
    };
}

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Structured actor admission outcome with no invalid rejected-without-reason state.</summary>
public readonly record struct ActorAdmissionResult
{
    ActorAdmissionResult(ActorAdmissionReason reason) => Reason = reason;

    public bool Accepted => Reason == ActorAdmissionReason.None;
    public ActorAdmissionReason Reason { get; }
    public static ActorAdmissionResult AcceptedResult => default;

    public static ActorAdmissionResult Rejected(ActorAdmissionReason reason)
    {
        if (reason == ActorAdmissionReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason));
        return new ActorAdmissionResult(reason);
    }
}

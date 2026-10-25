namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Controls whether aggregate actor admission is disabled, measured, or enforced.</summary>
public enum ActorAdmissionMode
{
    Disabled,
    ObserveOnly,
    Enforce
}

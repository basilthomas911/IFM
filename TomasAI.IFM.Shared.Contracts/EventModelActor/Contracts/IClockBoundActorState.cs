namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Binds a newly resolved state to its owning application's business clock before use.</summary>
public interface IClockBoundActorState
{
    /// <summary>Sets the instance clock used only when staging new events; replay never creates provenance.</summary>
    void BindClock(TimeProvider clock);
}

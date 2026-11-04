namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Declares that replay changes only the actor identity and committed stream version.</summary>
/// <remarks>Reducers opting in must not retain business data; creation and the captured head fully restore them.</remarks>
public interface IStatelessCreationSnapshotState { }

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Allows an identical command to resume an audit reservation that has no committed source events.</summary>
/// <remarks>The atomic transaction must lock the audit row, verify the complete retry identity and prove no event was committed.</remarks>
public interface IRecoverableCommandAudit : ICommandRetryIdentity;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Opt-in retry comparison. Return a same-type copy that removes only per-attempt
/// tracing metadata; retain payload, routing and access identity. The original
/// command remains the audit record. Duplicates must run validation/authorization
/// and committed-state replay handling rather than receive an automatic success.
/// </summary>
public interface ICommandRetryIdentity : ICommand
{
    ICommand ForRetryIdentity();
}

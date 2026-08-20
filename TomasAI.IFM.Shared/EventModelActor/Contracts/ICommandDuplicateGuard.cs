using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Atomically reserves command identifiers before actor validation or business processing begins.
/// </summary>
public interface ICommandDuplicateGuard
{
    /// <summary>
    /// Returns <see langword="true"/> only for the caller that durably reserved the command identifier.
    /// A <see langword="false"/> result is an idempotent duplicate and must not be processed again.
    /// </summary>
    ValueTask<bool> TryAcceptAsync(
        ICommand command,
        CancellationToken cancellationToken = default);
}

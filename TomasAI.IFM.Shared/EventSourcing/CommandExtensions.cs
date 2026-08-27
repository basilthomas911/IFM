namespace TomasAI.IFM.Shared.EventSourcing;
public static class CommandExtensions
{
    public static ServiceResult<GuidResult> UpdateFailed(this ICommand e, string errorMessage)
      => new ServiceFailed<GuidResult>(e.ErrorCode, errorMessage, new GuidResult(e.CommandId));

    public static ServiceResult<GuidResult> UpdatedOk(this ICommand e, Action updateAction)
    {
        updateAction?.Invoke();
        return new ServiceOk<GuidResult>(new GuidResult(e.CommandId));
    }

    /// <summary>
    /// Executes an event-sourced state update and returns the standard command acknowledgement.
    /// </summary>
    /// <param name="command">The command whose identity is returned.</param>
    /// <param name="updateAction">The state update to execute.</param>
    /// <returns>A successful result carrying the originating command identity.</returns>
    /// <remarks>
    /// A <see langword="false"/> update result represents an idempotent no-op in several aggregates and is not a
    /// command-processing failure. Validation and exceptional failures are reported by their dedicated paths.
    /// </remarks>
    public static ServiceResult<GuidResult> UpdateResult(
        this ICommand command,
        Func<bool> updateAction)
    {
        _ = updateAction.Invoke();
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }
}


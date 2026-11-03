namespace TomasAI.IFM.Shared.EventSourcing;

public static class CommandExtensions
{

    extension(ICommand e)
    {
        public ServiceResult<GuidResult> UpdatedOk(Action updateAction)
        {
            ArgumentNullException.ThrowIfNull(e);
            updateAction?.Invoke();
            return new ServiceOk<GuidResult>(new GuidResult(e.CommandId));
        }

        public ServiceResult<GuidResult> UpdateFailed(string errorMessage)

        {
            ArgumentNullException.ThrowIfNull(e);
            return new ServiceFailed<GuidResult>(e.ErrorCode, errorMessage, new GuidResult(e.CommandId));
        }

        public bool UpdateFailed(ref string errorMessage, string newErrorMsg)
        {
            errorMessage = newErrorMsg;
            return false;
        }
    }

    /// <param name="command">
    /// The command whose identity is returned.
    /// </param>
    extension(ICommand command)
    {
        /// <summary>
        /// Executes an event-sourced state update and returns the standard command acknowledgement.
        /// </summary>
        ///
        /// <param name="updateAction">The state update to execute.</param>
        /// <returns>A successful result carrying the originating command identity.</returns>
        /// <remarks>
        /// A <see langword="false"/> update result represents an idempotent no-op in several aggregates and is not a
        /// command-processing failure. Validation and exceptional failures are reported by their dedicated paths.
        /// </remarks>
        public ServiceResult<GuidResult> UpdateResult(
            Func<bool> updateAction)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(updateAction);
            _ = updateAction.Invoke();
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        }
    }
}


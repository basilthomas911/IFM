namespace TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;

/// <summary>
/// Specifies the possible outcomes for a command execution.
/// </summary>
/// <remarks>Use this enumeration to determine the current state or final result of a command. The values indicate
/// whether the command is still in progress, has completed successfully, has failed, or encountered a failure
/// specifically during the denormalization process.</remarks>
public enum CommandStatus
{
    InProgress,
    Completed,
    Failed,
    DenormalizerFailed
}

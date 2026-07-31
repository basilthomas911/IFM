namespace TomasAI.IFM.Shared.EventSourcing.SetParameters;

/// <summary>
/// Represents a set of parameters used to log the execution details of a command within an event sourcing system.
/// </summary>
/// <remarks>Use this record struct to encapsulate all necessary information for structured logging of command
/// executions. This enables consistent auditing and traceability of command activity within the system.</remarks>
/// <param name="CommandId">The unique identifier of the command being logged.</param>
/// <param name="StreamId">The identifier of the stream associated with the command execution.</param>
/// <param name="AggregateName">The name of the aggregate that receives the command.</param>
/// <param name="CommandName">The name of the command that was executed.</param>
/// <param name="CommandTimestamp">The timestamp indicating when the command was executed, typically in ISO 8601 format.</param>
/// <param name="CommandStatus">The processing status of the command.</param>
/// <param name="CommandData">The data associated with the command, containing relevant information for logging purposes.</param>
public readonly record struct InsertCommandLogSetParameter(
    Guid CommandId,
    string StreamId,
    string AggregateName,
    string CommandName,
    string CommandTimestamp,
    string CommandStatus,
    string CommandData
);

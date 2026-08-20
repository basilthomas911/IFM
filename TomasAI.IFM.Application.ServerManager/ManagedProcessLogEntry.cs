using System;

namespace TomasAI.IFM.Application.ServerManager;

public sealed record ManagedProcessLogEntry(
    DateTimeOffset Timestamp,
    string ProcessKey,
    string ProcessName,
    ManagedProcessLogStream Stream,
    string Message);

public enum ManagedProcessLogStream
{
    Manager,
    Lifecycle,
    StandardOutput,
    StandardError
}

using System;

namespace TomasAI.IFM.Application.ServerManager;

public sealed class StatusLog
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string ProcessName { get; init; }

    public required ManagedProcessLogStream Stream { get; init; }

    public required string Message { get; init; }
}

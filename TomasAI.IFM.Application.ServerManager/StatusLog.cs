using System;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed class StatusLog
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string ProcessName { get; init; }

    public required ManagedProcessLogStream Stream { get; init; }

    public required string Message { get; init; }
}

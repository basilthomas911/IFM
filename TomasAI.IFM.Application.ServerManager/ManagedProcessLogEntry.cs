using System;

namespace TomasAI.IFM.Application.ServerManager;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]

public sealed record ManagedProcessLogEntry(
    DateTimeOffset Timestamp,
    string ProcessKey,
    string ProcessName,
    ManagedProcessLogStream Stream,
    string Message,
    Exception? Exception = null);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="Preserve the existing public host, configuration and reflection contracts used by external test assemblies.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1711",Justification="Preserve the established public contract or xUnit collection identity.")]

public enum ManagedProcessLogStream
{
    Manager,
    Lifecycle,
    StandardOutput,
    StandardError
}

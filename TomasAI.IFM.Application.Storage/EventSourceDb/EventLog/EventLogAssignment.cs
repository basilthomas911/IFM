


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct EventLogAssignment(long EventVersion, long StreamVersion);

using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProjectionMarker(long EventId, DurableProjectionRequirement Requirement);


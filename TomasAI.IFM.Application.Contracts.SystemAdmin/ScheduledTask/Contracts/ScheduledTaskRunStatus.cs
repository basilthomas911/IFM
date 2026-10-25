using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
public enum ScheduledTaskRunStatus : byte { Requested = 0, Admitted = 1, Running = 2, Succeeded = 3, Failed = 4, Uncertain = 5, Rejected = 6 }

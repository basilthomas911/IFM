using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
public enum ScheduledTaskInstallationStatus : byte { Pending = 0, Applied = 1, Failed = 2 }

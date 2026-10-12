using TomasAI.IFM.Domain.OptionPricer.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Model;

/// <summary>Immutable proposed business values; authoritative state remains owned by the command state.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal readonly record struct SpreadDistributionJobFailure(SpreadDistributionJobStatus JobStatus, bool Accepted);

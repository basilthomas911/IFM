using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Model;

/// <summary>Immutable proposed business values; authoritative state remains owned by the command state.</summary>
internal readonly record struct SpreadDistributionJobSubmission(SpreadDistributionJobReadModel SpreadDistributionJob, bool AlreadyInProgress);

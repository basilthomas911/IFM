using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Model;

/// <summary>Immutable proposed business values; authoritative state remains owned by the command state.</summary>
internal readonly record struct SpreadDistributionJobsRemoval(OptionTradeEntityId OptionTradeId, bool Accepted);

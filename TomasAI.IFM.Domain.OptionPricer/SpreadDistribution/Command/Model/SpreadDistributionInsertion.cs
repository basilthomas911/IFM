using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.Model;

/// <summary>Immutable proposed business values; authoritative state remains owned by the command state.</summary>
internal readonly record struct SpreadDistributionInsertion(SpreadDistributionReadModel PutSpreadDistribution, SpreadDistributionReadModel CallSpreadDistribution);

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer.CommandParameters;

/// <summary>
/// Represents the parameters required to insert paired put and call spread distributions for a specific trade context.
/// </summary>
/// <param name="PutSpreadDistribution">The put side spread distribution snapshot.</param>
/// <param name="CallSpreadDistribution">The call side spread distribution snapshot.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertSpreadDistributionParameter(
    SpreadDistributionReadModel PutSpreadDistribution,
    SpreadDistributionReadModel CallSpreadDistribution,
    int ErrorCode)
    : ICommandParameter;

using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
/// <summary>Calculates one pure Regime Discovery outcome from an immutable input.</summary>
public interface IRegimeDiscoveryCalculationModel
{
    /// <summary>Calculates a result without owning or mutating durable actor state.</summary>
    Task<RegimeDiscoveryResult> CalculateAsync(RegimeDiscoveryCalculationInput input, RegimeDiscoveryExecutionMode mode = RegimeDiscoveryExecutionMode.Sequential, CancellationToken cancellationToken = default);
}

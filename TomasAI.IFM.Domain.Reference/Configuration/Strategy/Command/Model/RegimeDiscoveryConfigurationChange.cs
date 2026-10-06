using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Model;
/// <summary>Immutable proposed configuration version and lifecycle timestamps.</summary>
internal sealed record RegimeDiscoveryConfigurationChange(RegimeDiscoveryParameterSet? ParameterSet = null, string Description = "", string CreatedBy = "", DateTime EffectiveFromUtc = default, DateTime RetiredAtUtc = default, string? RejectionReason = null);

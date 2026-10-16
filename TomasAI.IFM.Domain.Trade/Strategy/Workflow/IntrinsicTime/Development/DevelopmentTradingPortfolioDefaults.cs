using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
/// <summary>Stable, reviewable identities and policy values for the Development manifest.</summary>
public static class DevelopmentTradingPortfolioDefaults
{
    static readonly string[] OptionStrategyCodes = ["IronCondor", "CallVertical", "PutVertical"];
    public static Guid StableId(string value) => StrategyCatalogExamples.StableId("DevelopmentTradingPortfolio/" + value);
    public static Guid ConstructionId(TimeFrameType horizon) => StableId("construction/" + horizon);
    /// <summary>Exact deployment version for the current manifest.</summary>
    public static int DeploymentVersion(TimeFrameType horizon) => horizon == TimeFrameType.Daily ? 2 : 3;
    public static Guid ActivationId(int tradingYear, TimeFrameType horizon) => StableId($"activation/{tradingYear}/{horizon}");
    /// <summary>Manifest version three updates option cache digest pins to MessagePack; Daily futures remain at version two.</summary>
    public static SelectionConstructionPolicy[] ConstructionPolicies() => DevelopmentTradingPortfolioOptions.Horizons.Select(h => new SelectionConstructionPolicy { SchemaVersion = (short)(h == TimeFrameType.Daily ? 1 : 3), ParameterSetId = ConstructionId(h), Version = h == TimeFrameType.Daily ? 2 : 3, MaximumLegs = h == TimeFrameType.Daily ? 1 : 4, MinimumDaysToExpiry = h == TimeFrameType.Daily ? 7 : 5, MaximumDaysToExpiry = h == TimeFrameType.Daily ? 60 : 45, MinimumWingWidth = h == TimeFrameType.Daily ? 0 : 50, MaximumWingWidth = h == TimeFrameType.Daily ? 0 : 50, DeltaUnits = "UnderlyingEquivalent", MaximumDeltaTolerance = .10m, OptionChainCachePolicies = h == TimeFrameType.Daily ? null : OptionCacheProfiles().Select(p => new SelectionStructureOptionChainCachePolicy { StructureId = p.StrategyDefinitionId, StructureVersion = p.StrategyDefinitionVersion, Policy = new() { ParameterSetId = p.ParameterSetId, Version = p.Version, ConfigurationDigest = p.Hash() } }).ToArray() }).ToArray();
    /// <summary>User-approved ES profiles: 50-point widths; Iron Condor 30?45/preferred45 DTE, Verticals 5?10/preferred5 DTE.</summary>
    public static TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet[] OptionCacheProfiles() => OptionStrategyCodes.Select(code =>
    {
        var id = StrategyCatalogExamples.StableId("StrategyOptionChainCache/Development" + code);
        var structure = StrategyCatalogExamples.StableId("Development" + code);
        return (string.Equals(code, "IronCondor", StringComparison.Ordinal) ? TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.IronCondor(id, structure, 1) : TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.VerticalSpread(id, structure, 1))with
        {
            Enabled = true
        };
    }).ToArray();
    public static decimal[] CapitalAllocations(decimal total)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(total);
        var share = decimal.Round(total / 3m, 2, MidpointRounding.ToZero);
        return[share, share, total - share * 2];
    }
}

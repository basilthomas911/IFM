using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Marks a payload property as part of the schema from the specified version onward.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ParameterSchemaSinceAttribute(int version) : Attribute
{
    public int Version { get; } = version;
}




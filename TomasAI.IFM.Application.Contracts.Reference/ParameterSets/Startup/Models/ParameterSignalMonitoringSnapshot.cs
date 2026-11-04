using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
[MessagePackObject]
public sealed record ParameterSignalMonitoringSnapshot(
 [property: Key(0)] Guid RunId, [property: Key(1)] string ContractId, [property: Key(2)] DateTime CapturedAtUtc,
 [property: Key(3)] ParameterSignalMonitoringRow[] Rows);

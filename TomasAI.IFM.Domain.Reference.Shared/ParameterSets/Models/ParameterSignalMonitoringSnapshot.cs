using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public sealed record ParameterSignalMonitoringRow(
 [property:Key(0)] Guid AssignmentId,[property:Key(1)] long AssignmentRevision,
 [property:Key(2)] Guid RequirementId,[property:Key(3)] bool IsRequired,
 [property:Key(4)] int MaximumAgeSeconds,[property:Key(5)] RegimeDiscoverySignalObservation Observation);
[MessagePackObject]
public sealed record ParameterSignalMonitoringSnapshot(
 [property:Key(0)] Guid RunId,[property:Key(1)] string ContractId,[property:Key(2)] DateTime CapturedAtUtc,
 [property:Key(3)] ParameterSignalMonitoringRow[] Rows);

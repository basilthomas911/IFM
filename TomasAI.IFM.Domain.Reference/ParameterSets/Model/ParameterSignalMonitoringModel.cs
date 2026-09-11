using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;
/// <summary>Read-only current availability for monitored rows in an exact startup snapshot.</summary>
public static class ParameterSignalMonitoringModel
{
 public static async ValueTask<ParameterSignalMonitoringSnapshot> CaptureAsync(ParameterStartupRun run,string contractId,IRegimeDiscoveryMarketSignalSnapshotProvider provider,CancellationToken token)
 {
  ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
  var rows=new List<ParameterSignalMonitoringRow>();
  foreach(var assignment in run.Scopes.Where(x=>x.Enabled))
  {
   var version=run.Versions.Single(x=>x.Reference==assignment.Reference);
   var value=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(version.PayloadJson)!;
   var configured=(value.SchemaVersion==1?RegimeDiscoveryParameterModel.Defaults(value):value.SignalRequirements!)
    .Where(x=>x.Enabled&&x.Monitor).ToArray();
   if(configured.Length==0)continue;
   var request=new RegimeDiscoveryMarketSignalSnapshotRequest
   {
    MarketSeriesIdentity=MarketSeriesIdentity.ForContract(contractId),TargetHorizon=value.TargetHorizon,
    // Observation-only capture preserves unavailable rows instead of suppressing the full snapshot.
    Requirements=configured.Select(x=>new RegimeDiscoverySignalRequirement{Metric=x.Metric,TimeFrame=x.TimeFrame,IsRequired=false,
     CalculationConfigurationId=x.CalculationConfigurationId,MaximumAgeSeconds=x.MaximumAgeSeconds,Weight=1m}).ToArray(),
    FutureClockSkewSeconds=value.Freshness.FutureClockSkewSeconds,SupportedSchemaVersions=value.DataQuality.SupportedSignalSchemaVersions,
    ApprovedCalculationVersions=value.DataQuality.ApprovedCalculationVersions,CaptureAttempts=value.DataQuality.SnapshotCaptureAttempts
   };
   var captured=await provider.CaptureAsync(request,token);
   var observations=captured.Snapshot?.Observations??captured.Issues;
   foreach(var observation in observations)
   {
    var requirement=configured.Single(x=>x.Metric==observation.Metric&&x.TimeFrame==observation.SignalKey.TimeFrame);
    rows.Add(new(assignment.AssignmentId,assignment.Revision,requirement.RequirementId,requirement.IsRequired,requirement.MaximumAgeSeconds,observation));
   }
  }
  return new(run.RunId,contractId,DateTime.UtcNow,rows.ToArray());
 }
}

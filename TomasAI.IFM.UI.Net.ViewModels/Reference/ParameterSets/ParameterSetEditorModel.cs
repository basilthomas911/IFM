using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;

/// <summary>Working copy only; persisted versions are never edited in place.</summary>
public sealed class ParameterSetEditorModel
{
 public RegimeDiscoveryParameterSet? Parameters {get;private set;}
 public ParameterSetVersion? Selected {get;private set;}
 public long ExpectedRevision {get;private set;}
 public bool IsEditing {get;private set;}
 public bool AwaitingRefresh {get;private set;}
 public string Name {get;set;}="Regime Discovery Daily";
 public string Description {get;set;}=string.Empty;
 public bool CanEdit {get;private set;}=true;
 string? originalPayload;
 public Guid SetId=>Selected?.Reference.SetId??Parameters?.ParameterSetId??Guid.Empty;
 public void Clear(){Parameters=null;Selected=null;ExpectedRevision=0;IsEditing=false;AwaitingRefresh=false;CanEdit=true;originalPayload=null;Name=string.Empty;Description=string.Empty;}
 public void Load(ParameterSetVersion version,long revision)
 {
  Selected=version;ExpectedRevision=revision;Name=version.Name;Description=version.Description;
  originalPayload=version.PayloadJson;
  CanEdit=ParameterSchemaRegistry.Default.CanEditLosslessly(version.Reference.ComponentCode,version.SchemaVersion,version.PayloadJson);
  Parameters=CanEdit?JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(version.PayloadJson):null;
  IsEditing=false;AwaitingRefresh=false;
 }
 public void New(string payload){CanEdit=true;originalPayload=payload;Parameters=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(payload)??throw new InvalidDataException("Invalid draft.");Selected=null;ExpectedRevision=0;AwaitingRefresh=false;Name=$"Regime Discovery {Parameters.TargetHorizon}";Description=string.Empty;IsEditing=true;}
 public void AcknowledgeSave(){IsEditing=false;AwaitingRefresh=true;}
 public void BeginEdit(string? upgradedPayload=null)
 {
  if(!CanEdit)throw new InvalidOperationException("This payload contains unsupported schema or fields and is read-only.");
  if(AwaitingRefresh)throw new InvalidOperationException("Refresh to load the committed version before editing again.");
  if(Parameters is null)throw new InvalidOperationException("Select a version first.");
  if(upgradedPayload is not null)
  {
   var upgraded=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(upgradedPayload)??throw new InvalidDataException("Invalid upgrade preview.");
   if(upgraded.ParameterSetId!=Parameters.ParameterSetId||upgraded.TargetHorizon!=Parameters.TargetHorizon||upgraded.SchemaVersion!=ParameterSchemaRegistry.CurrentRegimeSchemaVersion)
    throw new InvalidDataException("Upgrade preview does not match the selected parameter set.");
   Parameters=upgraded;
  }
  IsEditing=true;
 }

 public void SetSignals(RegimeDiscoverySignalConfiguration[] rows)
 {if(!IsEditing||Parameters is null)throw new InvalidOperationException("The version is read-only.");Parameters=Parameters with {SignalRequirements=rows};}
 public void SetMetrics(RegimeDiscoverySignalMetricConfiguration[] signals,
  RegimeDiscoveryObservationMetricConfiguration[] observations)
 {
  if(!IsEditing||Parameters is null)throw new InvalidOperationException("The version is read-only.");
  var legacy=RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(
   Parameters.ParameterSetId,signals,observations,Parameters.Horizon);
  Parameters=Parameters with {SignalMetrics=signals,ObservationMetrics=observations,SignalRequirements=legacy};
 } public void SetFields(IEnumerable<ParameterField> fields)
 {
  if(!IsEditing)throw new InvalidOperationException("The version is read-only.");
  Parameters=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(ParameterFieldEditorModel.Apply(Payload(),fields))??throw new InvalidDataException("Invalid parameters.");
 }
 public string Payload()=>!IsEditing&&originalPayload is not null?originalPayload:JsonSerializer.Serialize(Parameters??throw new InvalidOperationException("No draft."));
}


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
 string? workingPayload;
 string componentCode=string.Empty;
 public bool HasPayload=>workingPayload is not null;
 public string ComponentCode=>componentCode;
 public int SchemaVersion=>Selected?.SchemaVersion??ReadInt32("SchemaVersion");
 public Guid SetId=>Selected?.Reference.SetId??ReadGuid("ParameterSetId");
 public void Clear(){Parameters=null;Selected=null;ExpectedRevision=0;IsEditing=false;AwaitingRefresh=false;CanEdit=true;originalPayload=null;workingPayload=null;componentCode=string.Empty;Name=string.Empty;Description=string.Empty;}
 public void Load(ParameterSetVersion version,long revision)
 {
  Selected=version;ExpectedRevision=revision;Name=version.Name;Description=version.Description;
  originalPayload=version.PayloadJson;workingPayload=version.PayloadJson;componentCode=version.Reference.ComponentCode;
  CanEdit=ParameterSchemaRegistry.Default.CanEditLosslessly(version.Reference.ComponentCode,version.SchemaVersion,version.PayloadJson);
  Parameters=CanEdit&&version.Reference.ComponentCode==ParameterSchemaRegistry.RegimeComponent
   ?JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(version.PayloadJson):null;
  IsEditing=false;AwaitingRefresh=false;
 }
 public void New(string payload,string componentCode,string componentName)
 {
  CanEdit=ParameterSchemaRegistry.Default.CanEditLosslessly(componentCode,ReadInt32(payload,"SchemaVersion"),payload);
  if(!CanEdit)throw new InvalidDataException("The generated draft is not supported by its registered editor.");
  originalPayload=payload;workingPayload=payload;this.componentCode=componentCode;
  Parameters=componentCode==ParameterSchemaRegistry.RegimeComponent
   ?JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(payload)??throw new InvalidDataException("Invalid draft.")
   :null;
  Selected=null;ExpectedRevision=0;AwaitingRefresh=false;
  Name=Parameters is null?componentName:$"Regime Discovery {Parameters.TargetHorizon}";
  Description=string.Empty;IsEditing=true;
 }
 public void AcknowledgeSave(){IsEditing=false;AwaitingRefresh=true;}
 public void BeginEdit(string? upgradedPayload=null)
 {
  if(!CanEdit)throw new InvalidOperationException("This payload contains unsupported schema or fields and is read-only.");
  if(AwaitingRefresh)throw new InvalidOperationException("Refresh to load the committed version before editing again.");
  if(workingPayload is null)throw new InvalidOperationException("Select a version first.");
  if(upgradedPayload is not null)
  {
   if(ReadGuid(upgradedPayload,"ParameterSetId")!=SetId)
    throw new InvalidDataException("Upgrade preview does not match the selected parameter set.");
   workingPayload=upgradedPayload;
   if(componentCode==ParameterSchemaRegistry.RegimeComponent)
   {
    var upgraded=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(upgradedPayload)??throw new InvalidDataException("Invalid upgrade preview.");
    if(Parameters is null||upgraded.TargetHorizon!=Parameters.TargetHorizon||upgraded.SchemaVersion!=ParameterSchemaRegistry.CurrentRegimeSchemaVersion)
     throw new InvalidDataException("Upgrade preview does not match the selected parameter set.");
    Parameters=upgraded;
   }
  }
  IsEditing=true;
 }

 public void SetSignals(RegimeDiscoverySignalConfiguration[] rows)
 {if(!IsEditing||Parameters is null)throw new InvalidOperationException("The version is read-only.");Parameters=Parameters with {SignalRequirements=rows};workingPayload=JsonSerializer.Serialize(Parameters);}
 public void SetMetrics(RegimeDiscoverySignalMetricConfiguration[] signals,
  RegimeDiscoveryObservationMetricConfiguration[] observations)
 {
  if(!IsEditing||Parameters is null)throw new InvalidOperationException("The version is read-only.");
  var legacy=RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(
   Parameters.ParameterSetId,signals,observations,Parameters.Horizon);
  Parameters=Parameters with {SignalMetrics=signals,ObservationMetrics=observations,SignalRequirements=legacy};
  workingPayload=JsonSerializer.Serialize(Parameters);
 } public void SetFields(IEnumerable<ParameterField> fields)
 {
  if(!IsEditing)throw new InvalidOperationException("The version is read-only.");
  workingPayload=ParameterFieldEditorModel.Apply(Payload(),fields);
  if(componentCode==ParameterSchemaRegistry.RegimeComponent)
   Parameters=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(workingPayload)??throw new InvalidDataException("Invalid parameters.");
 }
 public string Payload()=>!IsEditing&&originalPayload is not null?originalPayload:workingPayload??throw new InvalidOperationException("No draft.");
 int ReadInt32(string property)=>workingPayload is null?0:ReadInt32(workingPayload,property);
 Guid ReadGuid(string property)=>workingPayload is null?Guid.Empty:ReadGuid(workingPayload,property);
 static int ReadInt32(string payload,string property){using var json=JsonDocument.Parse(payload);return json.RootElement.TryGetProperty(property,out var value)&&value.TryGetInt32(out var result)?result:0;}
 static Guid ReadGuid(string payload,string property){using var json=JsonDocument.Parse(payload);return json.RootElement.TryGetProperty(property,out var value)&&value.TryGetGuid(out var result)?result:Guid.Empty;}
}


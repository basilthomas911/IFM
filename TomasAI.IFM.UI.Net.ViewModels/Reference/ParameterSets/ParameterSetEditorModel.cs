using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;

/// <summary>Detached concrete working copies. Saved versions are never edited in place.</summary>
public sealed class ParameterSetEditorModel
{
    ParameterSetValue? original,working;
    string componentCode=string.Empty;
    public RegimeDiscoveryParameterSet? Parameters=>working is RegimeDiscoveryParameterValue regime?regime.Value:null;
    public ParameterSetVersion? Selected{get;private set;}
    public long ExpectedRevision{get;private set;}
    public bool IsEditing{get;private set;}
    public bool AwaitingRefresh{get;private set;}
    public string Name{get;set;}=string.Empty;
    public string Description{get;set;}=string.Empty;
    public bool CanEdit{get;private set;}=true;
    public string? HistoricalDocument{get;private set;}
    public bool HasPayload=>working is not null||HistoricalDocument is not null;
    public string ComponentCode=>componentCode;
    public int SchemaVersion=>Selected?.SchemaVersion??working?.SchemaVersion??0;
    public Guid SetId=>Selected?.Reference.SetId??working?.SetId??Guid.Empty;
    public void Clear(){original=working=null;Selected=null;ExpectedRevision=0;IsEditing=AwaitingRefresh=false;CanEdit=true;HistoricalDocument=null;componentCode=Name=Description=string.Empty;}
    public void Load(ParameterSetVersion version,long revision)
    {
        Clear();Selected=version;ExpectedRevision=revision;Name=version.Name;Description=version.Description;componentCode=version.Reference.ComponentCode;
        if(version.Parameters is null&&!ParameterSchemaRegistry.Default.CanEditLosslessly(componentCode,version.SchemaVersion,version.PayloadJson))
        {CanEdit=false;HistoricalDocument=version.PayloadJson;return;}
        original=ParameterValueCodec.Snapshot(version.Values());working=ParameterValueCodec.Snapshot(original);
    }
    public void New(ParameterSetValue value,string componentCode,string componentName)
    {
        Clear();if(value.ComponentCode!=componentCode)throw new InvalidDataException("Draft component does not match selection");
        this.componentCode=componentCode;original=ParameterValueCodec.Snapshot(value);working=ParameterValueCodec.Snapshot(value);
        Name=Parameters is null?componentName:$"Regime Discovery {Parameters.TargetHorizon}";IsEditing=true;
    }
    /// <summary>Imports a supported historical draft for migration only.</summary>
    public void New(string historical,string component,string name)
    {
        var value=ParameterHistoryReader.ReadValues(component,historical);
        if(!ParameterSchemaRegistry.Default.CanEditLosslessly(component,value.SchemaVersion,historical))throw new InvalidDataException("Unsupported historical fields");
        New(value,component,name);
    }
    public void AcknowledgeSave(){IsEditing=false;AwaitingRefresh=true;}
    public void BeginEdit(ParameterSetValue? upgraded=null)
    {
        if(!CanEdit)throw new InvalidOperationException("Unsupported historical fields are read-only");
        if(AwaitingRefresh)throw new InvalidOperationException("Refresh the committed version before editing again");
        if(working is null)throw new InvalidOperationException("Select a version first");
        if(upgraded is not null)
        {
            if(upgraded.SetId!=SetId||upgraded.ComponentCode!=componentCode)throw new InvalidDataException("Upgrade does not match selected parameter set");
            if(Parameters is {} before&&upgraded is RegimeDiscoveryParameterValue after&&before.TargetHorizon!=after.Value.TargetHorizon)throw new InvalidDataException("Upgrade horizon mismatch");
            working=ParameterValueCodec.Snapshot(upgraded);
        }
        IsEditing=true;
    }
    public void BeginEdit(string historical)=>BeginEdit(ParameterHistoryReader.ReadValues(componentCode,historical));
    void DemandEdit(){if(!IsEditing)throw new InvalidOperationException("The version is read-only");}
    public void SetSignals(RegimeDiscoverySignalConfiguration[] rows)
    {DemandEdit();working=ParameterSetValue.From((Parameters??throw new InvalidOperationException("Select Regime Discovery")) with{SignalRequirements=rows});}
    public void SetMetrics(RegimeDiscoverySignalMetricConfiguration[] signals,RegimeDiscoveryObservationMetricConfiguration[] observations)
    {
        DemandEdit();var parameters=Parameters??throw new InvalidOperationException("Select Regime Discovery");
        var legacy=RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(parameters.ParameterSetId,signals,observations,parameters.Horizon);
        working=ParameterSetValue.From(parameters with{SignalMetrics=signals,ObservationMetrics=observations,SignalRequirements=legacy});
    }
    public void SetFields(IEnumerable<ParameterField> fields){DemandEdit();working=ParameterFieldEditorModel.Apply(Payload(),fields);}
    public ParameterSetValue Payload()=>ParameterValueCodec.Snapshot((!IsEditing?original:working)??throw new InvalidOperationException("No supported parameter values"));
}

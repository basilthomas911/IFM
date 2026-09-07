using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
/// <summary>Exact activation configuration stored in the existing workflow parameter table.</summary>
public sealed record TradeSelectionActivation
{
    [JsonRequired] public short SchemaVersion {get;init;}
    [JsonRequired] public Guid ParameterSetId {get;init;}
    [JsonRequired] public int Version {get;init;}
    [JsonRequired] public int PortfolioId {get;init;}
    [JsonRequired] public int? FundId {get;init;}
    [JsonRequired] public string InstrumentRoot {get;init;}=string.Empty;
    [JsonRequired] public TimeFrameType TargetHorizon {get;init;}
    [JsonRequired] public SelectionPipelinePolicyReference SelectionPolicyReference {get;init;}=new();
    static readonly JsonSerializerOptions Options=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static TradeSelectionActivation Read(string json)
    {
        TradeSelectionPolicy.CheckJson(json);
        var p=JsonSerializer.Deserialize<TradeSelectionActivation>(json,Options)??throw new ArgumentException("Missing activation.");p.Validate();return p;
    }
    public string Serialize(){Validate();return JsonSerializer.Serialize(this,Options);}
    public string Hash()=>TradeSelectionPolicy.HashJson(Serialize());
    public void Validate()
    {
        TradeSelectionContracts.Require(SchemaVersion==1 && ParameterSetId!=Guid.Empty && Version>0 && PortfolioId>0 && (FundId is null or >0) && InstrumentRoot=="ES" && TradeSelectionPolicy.IsHorizon(TargetHorizon),"TS.CONFIG.ACTIVATION","Invalid workflow activation.");
        TradeSelectionContracts.Require(SelectionPolicyReference is {Kind:CatalogPipelineParameterKind.TradeSelection,Version:>0,Role:""} r && r.Id!=Guid.Empty && r.PayloadSha256.Length==64 && r.PayloadSha256.All(Uri.IsHexDigit),"TS.CONFIG.ACTIVATION","Activation requires an exact selector policy reference.");
    }
}

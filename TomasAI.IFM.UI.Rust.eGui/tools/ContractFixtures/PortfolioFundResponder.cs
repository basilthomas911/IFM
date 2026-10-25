using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
internal sealed partial class PortfolioResponder {
 bool HasFundState=>Mode.StartsWith("Fund",StringComparison.Ordinal);
 readonly List<FundMandateReadModel> fundVersions=[];
 FundMandateReadModel? createdFund;
 bool fundAttached;
 long fundRevision=4;
 FundAllocationReadModel? fundAllocation;
 FundRiskEnvelopeReadModel? fundEnvelope;
 readonly List<FundTradeTemplateAssignmentReadModel> fundAssignments=[];
 public static LookupDefinitionReadModel[] LookupChoices(string group){
  var values=group switch{"AssetTypes"=>new[]{"Futures","FuturesOption","Unavailable"},"Directions"=>Enum.GetNames<MarketConditionDirection>().Where(v=>v!="Undefined").ToArray(),"MarketConditions"=>Enum.GetNames<MarketConditionType>().Where(v=>v!="Undefined").ToArray(),_=>throw new InvalidDataException("Unknown lookup group")};
  return values.Select((v,i)=>new LookupDefinitionReadModel(i+1,group,v,v,"Fixture choice",i,v!="Unavailable",StrategyFixtures.At,StrategyFixtures.At)).ToArray();
 }
 public static FundMandateReadModel EditableFund=>PortfolioFixtures.Fund with{SchemaVersion=3,PermittedDirections=["Bullish"],PermittedConditions=[Enum.GetNames<MarketConditionType>().First(v=>v!="Undefined")],PermittedTradeStrategyFamilies=[TradeStrategyFamilyReference.From(Deployment)]};
 FundMandateReadModel[] FundRows()=>[..(fundVersions.Count==0?new[]{EditableFund}:fundVersions.ToArray()),..(fundAttached&&createdFund is not null?new[]{createdFund}:[])];
 string? ApplyFund(object command){
  switch(command){
   case CreateFundMandateCommand c:
    if(createdFund is not null)return "Duplicate mandate creation";
    if(c.Mandate.PortfolioId!=1||c.Mandate.FundId!=300||c.Mandate.FundMandateVersion!=1||c.Mandate.OperatingState!=FundOperatingState.Draft||c.Mandate.SchemaVersion!=3||c.Mandate.TradingYear!=2026||!DesktopFixtureMode.Clock(c.Mandate.CreatedOnUtc,StrategyFixtures.At)||c.IdempotencyKey==Guid.Empty||c.Mandate.PermittedTradeStrategyFamilies is not [var family]||family.CatalogDeployment!=Deployment.Key)return "Incorrect Fund draft DTO";
    createdFund=c.Mandate;return null;
   case AddFundToPortfolioCommand c:
    if(Mode=="FundAttachReject"){Mode="FundCommands";return "Fixture attachment rejected";}
    if(createdFund is null||c.FundId.FundId!=300||c.ExpectedPortfolioVersion!=adminRevision)return "Incorrect Fund attachment";fundAttached=true;adminRevision++;return null;
   case AddFundMandateVersionCommand c:
    var prior=FundRows().Last(p=>p.FundId==c.Mandate.FundId);
    if(c.ExpectedVersion!=fundRevision||c.Mandate.FundMandateVersion!=prior.FundMandateVersion+1||c.Mandate.SchemaVersion!=3)return "Incorrect Fund version/revision";
    if(fundVersions.Count==0)fundVersions.Add(EditableFund);fundVersions.Add(c.Mandate);fundRevision++;return null;
   case DelegateFundAllocationCommand c:
    if(c.ExpectedPortfolioVersion!=adminRevision||c.Allocation.PortfolioId!=1||c.Allocation.FundId!=2||c.Allocation.AllocationVersion!=(fundAllocation?.AllocationVersion??0)+1||c.Allocation.SourcePolicyId!=11||c.Allocation.TargetWeight!=.5m||c.Allocation.AllocatedCapital!=10000m||!DesktopFixtureMode.Clock(c.Allocation.EffectiveFromUtc,StrategyFixtures.At))return "Incorrect delegated allocation";
    fundAllocation=c.Allocation;adminRevision++;return null;
   case DelegateFundRiskEnvelopeCommand c:
    if(c.ExpectedPortfolioVersion!=adminRevision||c.Envelope.PortfolioId!=1||c.Envelope.FundId!=2||c.Envelope.EnvelopeVersion!=(fundEnvelope?.EnvelopeVersion??0)+1||c.Envelope.SourcePolicyVersion!=1||c.Envelope.SourcePolicyId!=11||c.Envelope.ExpiresAtUtc!=StrategyFixtures.At.AddDays(30)||c.Envelope.MaximumContracts!=5)return "Incorrect delegated envelope";
    fundEnvelope=c.Envelope;adminRevision++;return null;
   case AssignTradeTemplateCommand c:
    if(c.ExpectedVersion!=fundRevision||c.Assignment.AssignmentVersion!=fundRevision+1||c.Assignment.TradeTemplateId!=Deployment.Key.Id||c.Assignment.TradeTemplateVersion!=3||c.Assignment.TradeStrategyFamily?.CatalogDeployment!=Deployment.Key||c.Assignment.Enabled||c.Assignment.Priority!=2)return "Incorrect deployment assignment";
    fundAssignments.Add(c.Assignment);fundRevision++;return null;
   case ChangeFundOperatingStateCommand c:
    if(c.ExpectedVersion!=fundRevision||string.IsNullOrWhiteSpace(c.Reason))return "Incorrect Fund state revision";
    var current=FundRows().Last(p=>p.FundId==c.EntityId.FundId);if(fundVersions.Count==0)fundVersions.Add(EditableFund);fundVersions.RemoveAll(p=>p.FundId==current.FundId&&p.FundMandateVersion==current.FundMandateVersion);fundVersions.Add(current with{OperatingState=c.State});fundRevision++;return null;
   default:return "Unsupported Fund command";
  }
 }
 void ResetFunds(){fundVersions.Clear();createdFund=null;fundAttached=false;fundRevision=4;fundAllocation=null;fundEnvelope=null;fundAssignments.Clear();}
}

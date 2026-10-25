using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
/// <summary>Isolated typed Portfolio reads, with no database or real account access.</summary>
internal sealed partial class PortfolioResponder {
 public string Mode{get;set;}="Normal";
 readonly List<PortfolioFinancialPolicyReadModel> saved=[];
 readonly Dictionary<Guid,byte[]> completed=[];
 PortfolioReadModel parent=PortfolioFixtures.Portfolio;
 long parentRevision=3;
 int nextPolicy=100;
 public void Reset(){saved.Clear();completed.Clear();parent=PortfolioFixtures.Portfolio;parentRevision=3;nextPolicy=100;adminParent=PortfolioFixtures.Portfolio;adminRevision=3;ResetFunds();Mode="Normal";}
 public bool HasDeployments=>Mode is "Commands" or "CommandReject" or "LostAck" or "RefreshReject" || HasFundState;
 public static readonly StrategyDeploymentChoice Deployment=new(new(StrategyCatalogKind.Deployment,Guid.Parse("284155cd-5703-4fcf-a2bf-1efef055218d"),3),"FIXTURE-ES","Fixture ES deployment",CatalogLifecycleStatus.Published,TimeFrameType.Daily,[new(1,"ES","CME","USD")],["FuturesOption"],[],["IronCondor"]);
 // A typed, process-local state machine exercises commands without touching an account.
 public byte[]? Command(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result(Guid id)=>MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=true,ErrorMessage="",Value=new(id)},options);
  try {
   var verb=subject.Split('.')[2];
   var type=typeof(CreatePortfolioFinancialPolicyCommand).Assembly.GetTypes().Single(t=>!t.IsAbstract&&t.Namespace==typeof(CreatePortfolioFinancialPolicyCommand).Namespace&&t.GetField("Actor")?.GetRawConstantValue() as string=="PortfolioFinancialPolicyCommand"&&t.GetField("Verb")?.GetRawConstantValue() as string==verb);
   var command=MessagePackSerializer.Deserialize(type,bytes,options)!;
   object? Get(string name)=>type.GetProperty(name)?.GetValue(command);
   if(Get("Subject") is not ActorSubject route||route.ToString()!=subject||Get("EntityId") is not IActorEntityId entity||entity.Format()!=string.Join('.',subject.Split('.').Skip(3))||Get("CommandId") is not Guid id||id==Guid.Empty||Get("CorrelationId") is not Guid correlation||correlation==Guid.Empty||Get("RequestedOnUtc") is not DateTime at||!DesktopFixtureMode.Clock(at,StrategyFixtures.At)||Get("Access") is not PortfolioAccessContext access||access.Roles is not ["PortfolioAdministrator"]||Get("PostEvents") is not true)throw new InvalidDataException("Incorrect policy command envelope");
   if(completed.TryGetValue(id,out var original)){if(!original.SequenceEqual(bytes))throw new InvalidDataException("Changed retry payload");return Result(id);}
   if(Mode=="CommandReject")throw new InvalidDataException("Fixture policy validation rejection");
   var prior=saved.LastOrDefault();
   switch(command){
    case CreatePortfolioFinancialPolicyCommand c:
     if(c.IdempotencyKey==Guid.Empty||c.Policy.PolicyId<100||c.Policy.PolicyVersion!=1||c.Policy.PortfolioId!=1||c.EntityId.Format()!=$"1.{c.Policy.PolicyId}"||c.Policy.OperatingState!=PortfolioFinancialPolicyState.Draft)throw new InvalidDataException("Incorrect new policy identity");
     Validate(c.Policy);saved.Add(c.Policy with{AggregateRevision=1});break;
    case AddPortfolioFinancialPolicyVersionCommand c:
     prior=saved.LastOrDefault(p=>p.PolicyId==c.Policy.PolicyId)??throw new InvalidDataException("Missing policy");
     if(c.ExpectedVersion!=prior.AggregateRevision||c.Policy.PolicyVersion!=prior.PolicyVersion+1)throw new InvalidDataException("Incorrect policy revision");
     Validate(c.Policy);saved.Add(c.Policy with{AggregateRevision=prior.AggregateRevision+1});break;
    case ActivateAndAssignPortfolioFinancialPolicyCommand c:
     prior=saved.Single(p=>$"1.{p.PolicyId}"==c.EntityId.Format()&&p.PolicyVersion==c.PolicyVersion);
     if(c.ExpectedPolicyRevision!=prior.AggregateRevision||c.ExpectedPortfolioRevision!=parentRevision)throw new InvalidDataException("Incorrect activation revisions");
     saved.Remove(prior);saved.Add(prior with{OperatingState=PortfolioFinancialPolicyState.Active,AggregateRevision=prior.AggregateRevision+1});parent=parent with{ActivePolicyId=prior.PolicyId,ActivePolicyVersion=prior.PolicyVersion};parentRevision++;break;
    case RetirePortfolioFinancialPolicyCommand c:
     prior=saved.Single(p=>$"1.{p.PolicyId}"==c.EntityId.Format()&&p.PolicyVersion==c.PolicyVersion);
     if(c.ExpectedRevision!=prior.AggregateRevision)throw new InvalidDataException("Incorrect retirement revision");
     saved.Remove(prior);saved.Add(prior with{OperatingState=PortfolioFinancialPolicyState.Retired,AggregateRevision=prior.AggregateRevision+1});break;
    case DeleteDraftPortfolioFinancialPolicyCommand c:
     prior=saved.Last(p=>$"1.{p.PolicyId}"==c.EntityId.Format());
     if(c.ExpectedRevision!=prior.AggregateRevision||prior.OperatingState!=PortfolioFinancialPolicyState.Draft)throw new InvalidDataException("Incorrect draft deletion");saved.RemoveAll(p=>p.PolicyId==prior.PolicyId);break;
    default:throw new InvalidDataException("Unexpected policy command");
   }
   completed.Add(id,bytes.ToArray());
   if(Mode=="LostAck"){Mode="Commands";return null;}
   if(Mode=="RefreshReject")Mode="ReadReject";
   return Result(id);
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<Guid>{Success=false,ErrorCode=34002,ErrorMessage=error.Message},options);}
 }
 static void Validate(PortfolioFinancialPolicyReadModel policy){
  if(policy.Name!="Native edited policy"||policy.CapitalBase!=50000m||policy.MaximumRiskPerTrade!=1000m||policy.MaximumOpenPositions!=5||!DesktopFixtureMode.Clock(policy.CreatedOnUtc,StrategyFixtures.At)||policy.BaseCurrency!=PortfolioFixtures.Portfolio.BaseCurrency||policy.TradeFamilyLimits is not [var limit]||limit.CatalogDeployment!=Deployment.Key||!limit.Enabled||limit.MaximumRiskPerTrade!=750m)throw new InvalidDataException("Policy inputs differ from exact expected DTO");
 }
 public byte[] Handle(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;var pieces=subject.Split('.');
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try{
   if(Mode is "Reject" or "ReadReject")throw new InvalidDataException("Fixture Portfolio unavailable");
   var type=typeof(GetPortfoliosQuery).Assembly.GetTypes().Single(t=>!t.IsAbstract&&t.Namespace==typeof(GetPortfoliosQuery).Namespace&&t.GetField("Actor")?.GetRawConstantValue() as string==pieces[1]&&t.GetField("Verb")?.GetRawConstantValue() as string==pieces[2]);
   var query=MessagePackSerializer.Deserialize(type,bytes,options)!;
   object? Get(string name)=>type.GetProperty(name)?.GetValue(query);
   if(Get("Subject") is not ActorSubject route||route.ToString()!=subject||Get("EntityId") is not IActorEntityId id||id.Format()!=string.Join('.',pieces.Skip(3)))throw new InvalidDataException("Incorrect Portfolio request route/identity");
   var allocating=query is AllocatePortfolioBusinessIdQuery;
   if(Get("RequestedOnUtc") is not DateTime at||!DesktopFixtureMode.Clock(at,StrategyFixtures.At)||at.Kind!=DateTimeKind.Utc||Get("CorrelationId") is not Guid correlation||correlation==Guid.Empty||Get("Access") is not PortfolioAccessContext access||!access.Roles.SequenceEqual([allocating?"PortfolioAdministrator":"PortfolioReader"]))throw new InvalidDataException("Incorrect Portfolio API-clock/caller metadata");
   if(Get("PortfolioId") is int portfolio&&portfolio!=(HasAdminState?adminParent.PortfolioId:1))throw new InvalidDataException("Incorrect Portfolio scope");
   if(Get("FundId") is int fund&&fund!=2&&!(HasFundState&&fund==300))throw new InvalidDataException("Incorrect Fund scope");
   switch(pieces[2]){
    case "AllocatePortfolioBusinessId":if(Get("Kind") is not PortfolioBusinessIdentityKind allocatedKind||allocatedKind is not (PortfolioBusinessIdentityKind.Policy or PortfolioBusinessIdentityKind.Portfolio or PortfolioBusinessIdentityKind.Fund))throw new InvalidDataException("Incorrect identity kind");return Result(new PortfolioBusinessIdAllocation{Kind=allocatedKind,Value=allocatedKind==PortfolioBusinessIdentityKind.Policy?nextPolicy++:allocatedKind==PortfolioBusinessIdentityKind.Portfolio?200:300,CorrelationId=correlation});
    case "GetPortfolios":var selectedParent=HasAdminState?adminParent:parent;return Result(new PortfolioPage<PortfolioReadModel>{Items=Get("State") is int state&&state!=(int)selectedParent.OperatingState?[]:[selectedParent],PageSize=100});
    case "GetPortfolio":return Result(HasAdminState?adminParent:parent);
    case "GetFunds":return Result(new PortfolioPage<FundMandateReadModel>{Items=HasFundState?FundRows():HasAdminState&&adminParent.PortfolioId!=1?[]:[Mode=="WrongFund"?PortfolioFixtures.Fund with{PortfolioId=999}:PortfolioFixtures.Fund],PageSize=100});
    case "GetFund":return Result(HasFundState?FundRows().Last(p=>p.FundId==(int)Get("FundId")!):PortfolioFixtures.Fund);
    case "GetPortfolioRevision":return Result(new PortfolioAggregateRevision{PortfolioId=HasAdminState?adminParent.PortfolioId:1,Revision=HasAdminState?adminRevision:parentRevision,SourceEventId=30});
    case "GetFundRevision":return Result(new PortfolioAggregateRevision{PortfolioId=1,FundId=(int)Get("FundId")!,Revision=HasFundState?fundRevision:4,SourceEventId=40});
    case "GetFundAllocation":return Result<FundAllocationReadModel?>(HasFundState?fundAllocation:null);
    case "GetFundRiskEnvelope":return Result<FundRiskEnvelopeReadModel?>(HasFundState?fundEnvelope:null);
    case "GetFundTemplateAssignments":return Result(HasFundState?fundAssignments.ToArray():Array.Empty<FundTradeTemplateAssignmentReadModel>());
    case "GetPortfolioFinancialPolicies":return Result(new PortfolioPage<PortfolioFinancialPolicyReadModel>{Items=Mode=="Policy"?[PortfolioFixtures.Policy]:saved.ToArray(),PageSize=200});
    case "GetPortfolioFinancialPolicy":case "GetActivePortfolioFinancialPolicy":return Result<PortfolioFinancialPolicyReadModel?>(null);
    default:throw new InvalidDataException("Unknown fixture Portfolio query "+pieces[2]);
   }
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=34014,ErrorMessage=error.Message},options);}
 }
}

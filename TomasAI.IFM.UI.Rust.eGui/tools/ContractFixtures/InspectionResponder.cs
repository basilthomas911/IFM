using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
/// <summary>Read-only typed inspection endpoint for isolated native protocol tests.</summary>
internal sealed class InspectionResponder {
 public string Mode{get;set;}="Normal";
 public byte[] Handle(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try{
   if(Mode=="Reject")throw new InvalidDataException("Fixture inspection unavailable");
   if(subject.StartsWith("Query.RiskManagementQuery.GetRiskHistoryPage.")){
    var q=MessagePackSerializer.Deserialize<GetRiskHistoryPageQuery>(bytes,options);
    if(q.Subject.ToString()!=subject||q.PortfolioId!=1||q.FundId!=2||q.ValueDate!=new DateOnly(2026,10,26)||q.PageSize!=25||q.Access.Roles is not ["PortfolioReader"]||!q.Access.Principal.StartsWith("interactive:"))throw new InvalidDataException("Incorrect risk history scope or reader access");
    if(q.PagingState is not (null or "fixture-next"))throw new InvalidDataException("Incorrect risk continuation");
    var row=new RiskHistoryRow(1,2,q.ValueDate,StrategyFixtures.At,StrategyFixtures.View.WorkflowId.Value,InspectionFixtures.Invocation,9,"Rejected",2,"Fixture risk history","Daily",123,"IronCondor");
    return Result(new RiskHistoryPage(q.PagingState is null?[row]:[],q.PagingState is null||Mode=="RepeatCursor"?"fixture-next":null));
   }
   if(subject.StartsWith("Query.RiskManagementQuery.GetRiskInvocation.")){
    var q=MessagePackSerializer.Deserialize<GetRiskInvocationQuery>(bytes,options);
    if(q.WorkflowId!=StrategyFixtures.View.WorkflowId||q.InvocationId!=InspectionFixtures.Invocation||q.Access.Roles is not ["PortfolioReader"])throw new InvalidDataException("Incorrect risk invocation");
    var value=InspectionFixtures.Observation;
    if(Mode=="WrongIdentity")value=value with{Snapshot=value.Snapshot with{State=StrategyFixtures.View with{WorkflowId=new(Guid.NewGuid())}}};
    return Result(value);
   }
   var history=MessagePackSerializer.Deserialize<GetMarketConditionAssessmentHistoryQuery>(bytes,options);
   if(history.Subject.ToString()!=subject||history.MarketProfileId!="ES.Standard"||history.InstrumentRoot!="ES"||history.PageSize!=25||history.BeforeUtc!=DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc))throw new InvalidDataException("Incorrect observation history scope");
   return Result(Array.Empty<MarketConditionAssessmentCompletedEvent>());
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=3401,ErrorMessage=error.Message},options);}
 }
}

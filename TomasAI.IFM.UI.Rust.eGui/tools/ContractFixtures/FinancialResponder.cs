using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
/// <summary>Ledger read boundary only. This fixture cannot post or configure a real ledger.</summary>
internal sealed class FinancialResponder {
 public string Mode{get;set;}="Normal";
 public byte[] Handle(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try{
   if(Mode=="Reject")throw new InvalidDataException("Fixture ledger unavailable");
   if(!subject.Contains(".GetFundPerformanceMetrics.",StringComparison.Ordinal))return Ledger(subject,bytes);
   var q=MessagePackSerializer.Deserialize<GetFundPerformanceMetricsQuery>(bytes,options);
   if(q.Subject.ToString()!=subject||q.EntityId.PortfolioId!=1||q.Scope.PortfolioId!=1||q.Scope.FundId!=2||q.Scope.Access.Roles is not ["LedgerRead"]||q.Scope.Access.PortfolioIds is not [1]||!DesktopFixtureMode.Clock(q.RequestedAtUtc,StrategyFixtures.At)||q.RequestedAtUtc.Kind!=DateTimeKind.Utc||q.CorrelationId==Guid.Empty||q.Parameters.From!=new DateOnly(2026,1,1)||q.Parameters.Through!=new DateOnly(2026,10,26))throw new InvalidDataException("Incorrect canonical metrics request");
   return Result(new FinancialRead<FundPerformanceMetrics>(Mode=="NotFound"?FinancialReadStatus.NotFound:Mode=="Unavailable"?FinancialReadStatus.Unavailable:FinancialReadStatus.Found,Mode is "NotFound" or "Unavailable"?null:FinancialFixtures.Report,7,StrategyFixtures.At));
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<FundPerformanceMetrics>{Success=false,ErrorCode=34120,ErrorMessage=error.Message},options);}
 }
 byte[] Ledger(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;var pieces=subject.Split('.');
  byte[] Result<T>(T value) where T:class=>MessagePackSerializer.Serialize(new ServiceResult<FinancialRead<T>>{Success=true,ErrorMessage="",Value=FinancialLedgerFixtures.Read(value)},options);
  if(Mode=="LedgerReject")throw new InvalidDataException("Fixture ledger page unavailable");
  var type=typeof(GetAccountBalancesQuery).Assembly.GetTypes().Single(t=>!t.IsAbstract&&t.Namespace==typeof(GetAccountBalancesQuery).Namespace&&t.GetField("Actor")?.GetRawConstantValue() as string==pieces[1]&&t.GetField("Verb")?.GetRawConstantValue() as string==pieces[2]);
  var query=MessagePackSerializer.Deserialize(type,bytes,options)!;object? Get(string key)=>type.GetProperty(key)?.GetValue(query);
  var parent=query is GetFinancialLedgerConfigurationQuery or PrepareFinancialBookQuery or PrepareFinancialAuthorityQuery;
  if(Get("Subject") is not TomasAI.IFM.Shared.EventModelActor.ActorSubject route||route.ToString()!=subject||Get("Scope") is not FinancialReadScope scope||scope.PortfolioId!=1||scope.FundId!=(parent?null:2)||!scope.Access.Roles.SequenceEqual(new[]{"LedgerRead","LedgerPost","LedgerReverse","LedgerConfigure","LedgerPeriodReopen","LedgerImport"})||scope.Access.PortfolioIds is not [1]||Get("RequestedAtUtc") is not DateTime at||!DesktopFixtureMode.Clock(at,StrategyFixtures.At)||Get("CorrelationId") is not Guid id||id==Guid.Empty)throw new InvalidDataException("Incorrect scoped ledger query");
  switch(query){
   case GetAccountBalancesQuery:return Result(FinancialLedgerFixtures.Balance);
   case GetFundTransactionsPageQuery q:
    if(q.Parameters.PageSize!=100)throw new InvalidDataException("Incorrect financial page size");
    var transactions=FinancialLedgerFixtures.Transactions(q.Parameters.Cursor);
    if(Mode=="WrongCursor")transactions=transactions with{NextCursor=new(1,999,7,7,0,"Transactions")};
    if(Mode=="ChangedCut"&&q.Parameters.Cursor is not null)transactions=transactions with{AsOfRevision=8};
    return Result(transactions);
   case GetFundReservationsPageQuery q:if(q.Parameters.PageSize!=100)throw new InvalidDataException("Incorrect financial page size");return Result(FinancialLedgerFixtures.Reservations(q.Parameters.Cursor));
   case GetJournalQuery q:if(q.Parameters.JournalId!=10)throw new InvalidDataException("Incorrect journal identity");return Result(FinancialLedgerFixtures.Journal);
   case GetFinancialPostingConfigurationQuery q:return Result(FinancialLedgerFixtures.Posting(q.Parameters.AccountingDate));
   case GetFinancialLedgerConfigurationQuery:
    if(Mode=="NoLedger")return MessagePackSerializer.Serialize(new ServiceResult<FinancialRead<FinancialLedgerConfiguration>>{Success=true,ErrorMessage="",Value=new(FinancialReadStatus.NotFound,null,0,StrategyFixtures.At)},options);
    return Result(Mode=="LedgerClosed"?FinancialConfigurationFixtures.Ledger with{Periods=[new(FinancialConfigurationFixtures.PeriodId,new(2026,1,1),new(2026,12,31),2,"Closed")]}:FinancialConfigurationFixtures.Ledger);
   case PrepareFinancialBookQuery q:return Result(FinancialConfigurationFixtures.Setup(q.Parameters));
   case PrepareFinancialAuthorityQuery q:return Result(FinancialConfigurationFixtures.Authority(q.Parameters.PermitNewSpending));
   default:throw new InvalidDataException("Unexpected ledger read");
  }
 }
}

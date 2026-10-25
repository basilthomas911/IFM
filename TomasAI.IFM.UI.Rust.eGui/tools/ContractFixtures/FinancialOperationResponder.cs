using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
/// <summary>Isolated commit/receipt authority. No real ledger or posting API is
/// constructed; exact C# contracts and hashes validate the native request.</summary>
internal sealed class FinancialOperationResponder {
 public string Mode{get;set;}="Normal";
 public string LedgerMode{get;set;}="Normal";
 public int Dispatches{get;private set;}
 readonly Dictionary<Guid,(byte[] Request,FinancialOperationOutcome Outcome)> committed=[];
 public byte[]? Handle(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
  try{
   if(subject.StartsWith("Query.",StringComparison.Ordinal)){
    var q=MessagePackSerializer.Deserialize<GetPostingReceiptQuery>(bytes,options);
    if(q.Subject.ToString()!=subject||q.Scope.PortfolioId!=1||q.EntityId.PortfolioId!=1||!DesktopFixtureMode.Clock(q.RequestedAtUtc,StrategyFixtures.At)||q.CorrelationId==Guid.Empty||!q.Scope.Access.Roles.Contains("LedgerRead"))throw new InvalidDataException("Invalid financial receipt query");
    if(Mode=="Unavailable")return Result(new FinancialRead<FinancialOperationOutcome>(FinancialReadStatus.Unavailable,null,42,StrategyFixtures.At));
    if(committed.TryGetValue(q.Parameters.OperationId,out var found)){
     var outcome=found.Outcome;
     if(Mode=="Mismatch")outcome=outcome with{Posting=outcome.Posting is {} p?p with{InputHash="counterfeit"}:null,Configuration=outcome.Configuration is {} c?c with{InputHash="counterfeit"}:null};
     return Result(new FinancialRead<FinancialOperationOutcome>(FinancialReadStatus.Found,outcome,43,StrategyFixtures.At));
    }
    return Result(new FinancialRead<FinancialOperationOutcome>(FinancialReadStatus.NotFound,null,42,Mode=="Expired"?StrategyFixtures.At.AddMinutes(3):StrategyFixtures.At));
   }
   Dispatches++;
   if(subject=="Command.GeneralLedgerCommand.Post.1"){
    var c=MessagePackSerializer.Deserialize<PostFundTransactionCommand>(bytes,options);
    if(c.CommandId!=c.OperationId||c.Subject.ToString()!=subject||c.EntityId.PortfolioId!=1||c.PortfolioId!=1||c.InputSha256!=FinancialCanonicalHash.Request(c)||c.Body.BookId!=11||c.Body.FundId!=2||c.Body.Amount!=100.2500m||!DesktopFixtureMode.Clock(c.RequestedAtUtc,StrategyFixtures.At))throw new InvalidDataException("Invalid retained posting request");
    if(Mode=="Prepared"){
     var draft=new TomasAI.IFM.UI.Net.ViewModels.Portfolio.FinancialPostingDraft(LedgerTransactionKind.DepositConfirmed,100.25m,new(2026,10,26),new(2026,10,26),"Native cash movement","fixture/confirmed",true);
     var operation=c.OperationId;
     if(c.Body.Source.SourceContentHash!=FinancialCanonicalHash.Compute(new{draft,Principal=c.Access.Principal,operation})||c.Body.MovementEvidence.ContentHash!=FinancialCanonicalHash.Compute(new{draft,Principal=c.Access.Principal})||c.Body.PostingRule.RuleId!=FinancialLedgerFixtures.DepositRule.RuleId||c.Body.PostingRule.Version!=3||c.Body.PostingRule.ContentHash!=FinancialLedgerFixtures.DepositRule.ContentHash||c.Body.Authority!=FinancialLedgerFixtures.Posting(new(2026,10,26)).Authority||c.Body.Source.SourceEventId!=c.OperationId||c.Body.Source.SourceEntityId!="1.2"||c.Body.MovementEvidence.Status!=MovementStatus.Confirmed||c.ExpiresAtUtc!=StrategyFixtures.At.AddMinutes(2))throw new InvalidDataException("Prepared posting differs from managed preparation fingerprints or backend selections");
    }
    if(committed.TryGetValue(c.OperationId,out var prior)&&!prior.Request.SequenceEqual(bytes))throw new InvalidDataException("Changed original posting payload");
    if(Mode!="AckOnly")committed[c.OperationId]=(bytes.ToArray(),new(){Posting=new(){Id=Guid.NewGuid(),CommandId=c.CommandId,OperationId=c.OperationId,PortfolioId=1,EntityId=new(1),InputHash=c.InputSha256,CommittedAtUtc=StrategyFixtures.At,Receipt=new(){OperationId=c.OperationId,PortfolioId=1,BookId=11,FundId=2,TransactionId=100,FinancialRevision=43,InputHash=c.InputSha256,CommittedAtUtc=StrategyFixtures.At}}});
    if(Mode=="LostAck"){Mode="Normal";return null;}
    return Result(new GuidResult(c.CommandId));
   }
   if(subject=="Command.LedgerConfigurationCommand.Configure.1"){
    var c=MessagePackSerializer.Deserialize<ConfigureLedgerCommand>(bytes,options);
    if(c.CommandId!=c.OperationId||c.Subject.ToString()!=subject||c.PortfolioId!=1||c.EntityId.PortfolioId!=1||c.InputSha256!=FinancialCanonicalHash.Request(c))throw new InvalidDataException("Invalid retained configuration request");
    if(Mode=="Controls")ValidateControl(c);
    if(committed.TryGetValue(c.OperationId,out var prior)&&!prior.Request.SequenceEqual(bytes))throw new InvalidDataException("Changed original configuration payload");
    if(Mode!="AckOnly")committed[c.OperationId]=(bytes.ToArray(),new(){Configuration=new(){Id=Guid.NewGuid(),CommandId=c.CommandId,OperationId=c.OperationId,PortfolioId=1,EntityId=new(1),InputHash=c.InputSha256,CommittedAtUtc=StrategyFixtures.At,Receipt=new(c.OperationId,c.Body.BookId,c.Body.Action,43,StrategyFixtures.At,null,"Development")}});
    if(Mode=="LostAck"){Mode="Normal";return null;}
    return Result(new GuidResult(c.CommandId));
   }
   throw new InvalidDataException("Unexpected financial command route");
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=34120,ErrorMessage=error.Message},options);}
 }
 void ValidateControl(ConfigureLedgerCommand command){
  var scope=new FinancialReadScope{PortfolioId=1,Access=command.Access};
  var snapshot=LedgerMode=="LedgerClosed"?FinancialConfigurationFixtures.Ledger with{Periods=[new(FinancialConfigurationFixtures.PeriodId,new(2026,1,1),new(2026,12,31),2,"Closed")]}:FinancialConfigurationFixtures.Ledger;
  var read=FinancialLedgerFixtures.Read(snapshot);
  const string reason="Native ledger control";var at=StrategyFixtures.At;
  ConfigureLedgerCommand expected=command.Body.Action switch{
   LedgerConfigurationAction.CreateBook=>TomasAI.IFM.UI.Net.ViewModels.Portfolio.FinancialControlPreparation.CreateBook(scope,FinancialConfigurationFixtures.Setup(new("paper:fixture",new(2026,1,1),new(2026,12,31))).Draft!,reason,at),
   LedgerConfigurationAction.RefreshAuthority=>TomasAI.IFM.UI.Net.ViewModels.Portfolio.FinancialControlPreparation.RefreshAuthority(scope,FinancialLedgerFixtures.Read(FinancialConfigurationFixtures.Authority(true)),reason,at),
   LedgerConfigurationAction.AddAccountVersion=>TomasAI.IFM.UI.Net.ViewModels.Portfolio.FinancialControlPreparation.EditAccount(scope,read,101,PostingSide.Credit,false,reason,at),
   LedgerConfigurationAction.AddPostingRuleVersion=>TomasAI.IFM.UI.Net.ViewModels.Portfolio.FinancialControlPreparation.EditRule(scope,read,FinancialLedgerFixtures.DepositRule.RuleId,new(102,1),new(101,1),false,null,null,new(2026,10,26),reason,at),
   _=>TomasAI.IFM.UI.Net.ViewModels.Portfolio.FinancialControlPreparation.Create(scope,read,command.Body.Action,reason,at,FinancialConfigurationFixtures.PeriodId,new(2027,1,1),new(2027,12,31),101,FinancialLedgerFixtures.DepositRule.RuleId)
  };
  if(command.Body.Action==LedgerConfigurationAction.OpenPeriod){if(command.Body.PeriodId==Guid.Empty)throw new InvalidDataException("Missing new period identity");expected=expected with{Body=expected.Body with{PeriodId=command.Body.PeriodId}};}
  if(command.ExpectedFinancialRevision!=expected.ExpectedFinancialRevision||command.ExpiresAtUtc!=expected.ExpiresAtUtc||!MessagePackSerializer.Serialize(command.Body,MessagePackBinarySerializer.ContentOptions).SequenceEqual(MessagePackSerializer.Serialize(expected.Body,MessagePackBinarySerializer.ContentOptions)))throw new InvalidDataException("Configuration body/revision differs from unchanged managed preparation: "+command.Body.Action);
 }
}

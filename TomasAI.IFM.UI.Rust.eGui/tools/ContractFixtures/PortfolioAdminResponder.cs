using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
/// <summary>In-memory administration authority. Rejects malformed contracts and
/// verifies optimistic revisions independently from the native controller.</summary>
internal sealed partial class PortfolioResponder {
 bool HasAdminState=>Mode.StartsWith("Admin",StringComparison.Ordinal)||HasFundState;
 PortfolioReadModel adminParent=PortfolioFixtures.Portfolio;
 long adminRevision=3;
 public byte[]? AdminCommand(string subject,byte[] bytes){
  var options=MessagePackBinarySerializer.Options;
  byte[] Result(Guid id)=>MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=true,ErrorMessage="",Value=new(id)},options);
  try{
   var pieces=subject.Split('.');
   var type=typeof(CreatePortfolioCommand).Assembly.GetTypes().Single(t=>!t.IsAbstract&&t.Namespace==typeof(CreatePortfolioCommand).Namespace&&t.GetField("Actor")?.GetRawConstantValue() as string==pieces[1]&&t.GetField("Verb")?.GetRawConstantValue() as string==pieces[2]);
   var command=MessagePackSerializer.Deserialize(type,bytes,options)!;
   object? Get(string key)=>type.GetProperty(key)?.GetValue(command);
   if(Get("CommandId") is not Guid id||id==Guid.Empty||Get("Subject") is not ActorSubject route||route.ToString()!=subject||Get("EntityId") is not IActorEntityId entity||entity.Format()!=string.Join('.',pieces.Skip(3))||Get("RequestedOnUtc") is not DateTime at||!DesktopFixtureMode.Clock(at,StrategyFixtures.At)||Get("Access") is not PortfolioAccessContext access||access.Roles is not ["PortfolioAdministrator"]||Get("CorrelationId") is not Guid correlation||correlation==Guid.Empty)throw new InvalidDataException("Invalid admin command envelope");
   if(completed.TryGetValue(id,out var original)){if(!original.SequenceEqual(bytes))throw new InvalidDataException("Changed retry payload");return Result(id);}
   if(Mode=="AdminReject")throw new InvalidDataException("Fixture administration validation failure");
   if(HasFundState){var error=ApplyFund(command);if(error is not null)throw new InvalidDataException(error);completed.Add(id,bytes.ToArray());if(Mode=="FundAttachLostAck"&&command is AddFundToPortfolioCommand){Mode="FundCommands";return null;}return Result(id);}
   switch(command){
    case CreatePortfolioCommand c:
     if(c.Portfolio.PortfolioId!=200||c.Portfolio.PortfolioVersion!=1||c.Portfolio.Name!="Native created portfolio"||c.Portfolio.BaseCurrency!="USD"||c.IdempotencyKey==Guid.Empty||!DesktopFixtureMode.Clock(c.Portfolio.CreatedOnUtc,StrategyFixtures.At)||c.Portfolio.OperatingState!=PortfolioOperatingState.Draft||!c.Portfolio.BrokerAccountRefs.SequenceEqual(["paper:a","paper:b"]))throw new InvalidDataException("Unexpected Portfolio creation DTO");
     var helper=typeof(TomasAI.IFM.Application.Api.Nats.Client.PortfolioCommandApi).Assembly.GetType("TomasAI.IFM.Application.Api.Nats.Client.IdempotentCommandId")!;
     var expected=(Guid)helper.GetMethod("Create")!.MakeGenericMethod(typeof(PortfolioReadModel)).Invoke(null,[c.IdempotencyKey,c.Portfolio])!;
     if(expected!=c.CommandId)throw new InvalidDataException("Unstable create command identity");adminParent=c.Portfolio;adminRevision=1;break;
    case AddPortfolioVersionCommand c:
     if(c.ExpectedVersion!=adminRevision||c.Portfolio.PortfolioVersion!=adminParent.PortfolioVersion+1||c.Portfolio.PortfolioId!=adminParent.PortfolioId||c.Portfolio.Name!="Native version portfolio")throw new InvalidDataException("Unexpected Portfolio version/revision");adminParent=c.Portfolio;adminRevision++;break;
    case ChangePortfolioOperatingStateCommand c:
     if(c.ExpectedVersion!=adminRevision||string.IsNullOrWhiteSpace(c.Reason))throw new InvalidDataException("Unexpected Portfolio state revision/reason");adminParent=adminParent with{OperatingState=c.State};adminRevision++;break;
    case DeleteDraftPortfolioCommand c:
     if(c.ExpectedVersion!=adminRevision||adminParent.OperatingState!=PortfolioOperatingState.Draft||string.IsNullOrWhiteSpace(c.Reason))throw new InvalidDataException("Unexpected draft deletion");adminParent=PortfolioFixtures.Portfolio;adminRevision=3;break;
    default:throw new InvalidDataException("Unexpected administration command "+pieces[2]);
   }
   completed.Add(id,bytes.ToArray());
   if(Mode=="AdminLostAck"){Mode="AdminCommands";return null;}
   if(Mode=="AdminRefreshReject")Mode="ReadReject";
   return Result(id);
  }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<Guid>{Success=false,ErrorCode=34002,ErrorMessage=error.Message},options);}
 }
}

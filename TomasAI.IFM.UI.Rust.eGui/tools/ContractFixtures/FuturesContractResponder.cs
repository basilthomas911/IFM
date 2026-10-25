using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>In-memory typed protocol responder; never accesses a database or broker account.</summary>
internal sealed class FuturesContractResponder(FuturesContractV3ReadModel provider)
{
    readonly Dictionary<string,FuturesContractV3ReadModel> rows=new(){{provider.Id.Format(),provider}};
    readonly HashSet<Guid> submissions=[];
    public bool HoldReads {get;set;}
    // Each trade scenario has a frozen catalog independent of reference-import
    // scenarios that may have run earlier in the same isolated responder.
    public void Reset(){rows.Clear();rows[provider.Id.Format()]=provider;submissions.Clear();HoldReads=false;}
    public FuturesContractV3ReadModel? Find(string id)=>rows.Values.SingleOrDefault(row=>row.ContractId==id);
    public void ChangeDescription(string id){var old=Find(id)??throw new InvalidDataException("Missing fixture future");rows[old.Id.Format()]=old with{Description="Changed outside preview"};}
    public async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] payload,CancellationToken token)
    {
        var options=MessagePackBinarySerializer.Options;
        if(subject.StartsWith("Query.FuturesContractQuery.GetFuturesContract.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetFuturesContractQuery>(payload,options);
            if(query.Subject.ToString()!=subject||subject!=$"Query.FuturesContractQuery.GetFuturesContract.{query.ContractId}")throw new InvalidDataException("Invalid future identity lookup");
            var found=Find(query.ContractId);return MessagePackSerializer.Serialize(new ServiceResult<FuturesContractV3ReadModel>{Success=found is not null,Value=found,ErrorMessage=found is null?"Future not found":""},options);
        }
        if(subject.StartsWith("Query.FuturesContractQuery.GetFuturesContracts.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetFuturesContractsQuery>(payload,options);
            if(subject!="Query.FuturesContractQuery.GetFuturesContracts.none"||query.Subject.ToString()!=subject)throw new InvalidDataException("Invalid futures list route");
            return HoldReads?null:MessagePackSerializer.Serialize(new ServiceResult<FuturesContractV3ReadModel[]>{Success=true,ErrorMessage="",Value=rows.Values.ToArray()},options);
        }
        try{
            var verb=subject.Split('.')[2];ICommand<FuturesContractId> command;FuturesContractV3ReadModel? value;FuturesContractId original;bool overwrite;
            if(verb=="Add"){
                var parsed=MessagePackSerializer.Deserialize<AddFuturesContractCommand>(payload,options);command=parsed;value=parsed.Contract;original=value.Id;overwrite=parsed.Overwrite;
            }else if(verb=="Change"){
                var parsed=MessagePackSerializer.Deserialize<ChangeFuturesContractCommand>(payload,options);command=parsed;value=parsed.Contract;original=parsed.ContractId;overwrite=parsed.Overwrite;
            }else if(verb=="Remove"){
                var parsed=MessagePackSerializer.Deserialize<RemoveFuturesContractCommand>(payload,options);command=parsed;value=null;original=parsed.ContractId;overwrite=parsed.Overwrite;
            }else throw new InvalidDataException("Unknown futures command");
            if(!submissions.Add(command.CommandId))throw new InvalidDataException("Duplicate submission reached the backend");
            if(command.Subject.ToString()!=subject||subject!=$"Command.FuturesContractCommand.{verb}.{command.EntityId.Format()}"||(bool)command.GetType().GetProperty("PostEvents")!.GetValue(command)!||command.RouteTo!=BoundedContextName.FuturesContractBoundedContext||!overwrite)throw new InvalidDataException("Invalid futures command identity/metadata");
            if(value is not null&&!value.IsValid)throw new InvalidDataException("Invalid contract");
            if(verb!="Add"&&!rows.ContainsKey(original.Format()))throw new InvalidDataException("Contract no longer exists");
            if(value?.Description=="Reject")throw new InvalidDataException("Contract fixture rejected mutation");
            if(value?.ReviewState==ReferenceReviewState.Reviewed&&FuturesReferenceQualification.Errors(value).Count>0)throw new InvalidDataException("Reference is not qualified");
            if(verb=="Change"&&rows[original.Format()].SchemaVersion>0){
                var before=rows[original.Format()];
                if(value!.ContractId!=before.ContractId||value.Symbol!=before.Symbol||value.Multiplier!=before.Multiplier||value.DefinitionDigest!=before.DefinitionDigest||value.InstrumentId!=before.InstrumentId||value.TickSize!=before.TickSize)throw new InvalidDataException("Immutable provider evidence changed");
            }
            rows.Remove(original.Format());if(value is not null)rows[value.Id.Format()]=value;
            var now=new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc);
            var eventSubject=new ActorSubject(ActorType.Event,"FuturesContractEvent",verb=="Add"?"AddedComplete":verb=="Change"?"ChangedComplete":"RemovedComplete",command.EntityId.Format());
            object terminal=verb switch{
                "Add"=>new FuturesContractAddedCompleteEvent{Subject=eventSubject,EntityId=command.EntityId,CommandId=command.CommandId,Id=Guid.NewGuid(),ReceivedOn=now,Contract=value,CreatedOn=now,CreatedBy="Fixture"},
                "Change"=>new FuturesContractChangedCompleteEvent{Subject=eventSubject,EntityId=command.EntityId,CommandId=command.CommandId,Id=Guid.NewGuid(),ReceivedOn=now,Contract=value,OriginalContractId=original,UpdatedOn=now,UpdatedBy="Fixture"},
                _=>new FuturesContractRemovedCompleteEvent{Subject=eventSubject,EntityId=command.EntityId,CommandId=command.CommandId,Id=Guid.NewGuid(),ReceivedOn=now,ContractId=original,DeletedOn=now,DeletedBy="Fixture"}
            };
            if(value?.Description!="NoTerminal")await connection.PublishAsync(eventSubject.ToString(),MessagePackSerializer.Serialize(terminal.GetType(),terminal,options),cancellationToken:token);
            if(value?.Description=="SlowRefresh")HoldReads=true;
            return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=true,ErrorMessage="",Value=new(command.CommandId)},options);
        }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=6004,ErrorMessage=error.Message},options);}
    }
}

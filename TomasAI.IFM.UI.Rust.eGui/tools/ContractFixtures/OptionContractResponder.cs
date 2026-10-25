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

/// <summary>Versioned in-memory catalog for native option paging/command tests.</summary>
internal sealed class OptionContractResponder
{
    readonly Dictionary<string,FuturesOptionContractReadModel> rows=[];
    readonly HashSet<Guid> commands=[];
    int version=1;
    public string Mode {get;set;}="Normal";
    public OptionContractResponder(FuturesOptionContractReadModel provider)
    {
        rows[provider.ContractId]=provider;
        var expiry=new DateOnly(2027,3,19);
        for(int index=0;index<300;index++){
            var strike=100m+index;var id=FuturesOptionContractId.Create("ES",expiry,OptionType.Call,strike);
            rows[id]=new(){ContractId=id,Description="Catalog fixture",Symbol="ES",LocalSymbol=id,SecurityType="FOP",Currency="USD",Exchange="CME",Multiplier="50",ContractMonth=expiry,StrikePrice=(double)strike,StrikePriceDecimal=strike,OptionType="Call"};
        }
        var mes=FuturesOptionContractId.Create("MES",expiry,OptionType.Put,6500.25m);
        rows[mes]=new(){ContractId=mes,Description="MES fixture",Symbol="MES",LocalSymbol=mes,SecurityType="FOP",Currency="USD",Exchange="CME",Multiplier="5",ContractMonth=expiry,StrikePrice=6500.25,StrikePriceDecimal=6500.25m,OptionType="Put"};
    }
    public async Task<byte[]?> Handle(NatsConnection connection,string subject,byte[] payload,CancellationToken token)
    {
        var options=MessagePackBinarySerializer.Options;
        if(subject.StartsWith("Query.FuturesOptionContractQuery.GetFuturesOptionContract.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetFuturesOptionContractQuery>(payload,options);
            if(query.Subject.ToString()!=subject||subject!=$"Query.FuturesOptionContractQuery.GetFuturesOptionContract.{query.ContractId}")throw new InvalidDataException("Invalid option identity lookup");
            rows.TryGetValue(query.ContractId,out var found);return MessagePackSerializer.Serialize(new ServiceResult<FuturesOptionContractReadModel>{Success=found is not null,Value=found,ErrorMessage=found is null?"Option not found":""},options);
        }
        if(subject.StartsWith("Query.FuturesOptionContractQuery.GetFuturesOptionContractsPage.",StringComparison.Ordinal)){
            try{
                var query=MessagePackSerializer.Deserialize<GetFuturesOptionContractsPageQuery>(payload,options);var request=query.Request;request.Validate();
                if(query.Subject.ToString()!=subject||subject!=$"Query.FuturesOptionContractQuery.GetFuturesOptionContractsPage.{request.Symbol}"||request.PageSize!=200)throw new InvalidDataException("Invalid paged option request");
                if(Mode=="HoldReads")return null;
                int offset=0;
                if(request.ContinuationToken is{} cursor){
                    if(Mode=="ExpiredCursor")throw new InvalidDataException("Expired option cursor");
                    var parts=cursor.Split('|');
                    if(parts.Length!=4||parts[0]!=request.Symbol||parts[3]!="opaque.Ω"||!int.TryParse(parts[1],out offset)||!int.TryParse(parts[2],out var revision)||revision!=version)throw new InvalidDataException("Expired or cross-symbol option cursor");
                }
                var all=rows.Values.Where(row=>row.Symbol==request.Symbol).OrderBy(row=>row.ContractId,StringComparer.Ordinal).ToArray();
                var items=all.Skip(offset).Take(request.PageSize).ToArray();var next=offset+items.Length<all.Length?$"{request.Symbol}|{offset+items.Length}|{version}|opaque.Ω":null;
                if(Mode=="RepeatCursor"&&request.ContinuationToken is not null)next=request.ContinuationToken;
                if(Mode=="WrongSymbol"&&request.ContinuationToken is not null)items=[rows.Values.Single(row=>row.Symbol=="MES")];
                return MessagePackSerializer.Serialize(new ServiceResult<FuturesOptionContractPageReadModel>{Success=true,ErrorMessage="",Value=new(items,next)},options);
            }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<FuturesOptionContractPageReadModel>{Success=false,ErrorCode=1033,ErrorMessage=error.Message},options);}
        }
        try{
            var verb=subject.Split('.')[2];ICommand<FuturesOptionContractEntityId> command;FuturesOptionContractReadModel? value;string original;bool overwrite;
            if(verb=="Add"){var parsed=MessagePackSerializer.Deserialize<AddFuturesOptionContractCommand>(payload,options);command=parsed;value=parsed.Contract;original=value.ContractId;overwrite=parsed.Overwrite;}
            else if(verb=="Change"){var parsed=MessagePackSerializer.Deserialize<ChangeFuturesOptionContractCommand>(payload,options);command=parsed;value=parsed.Contract;original=parsed.ContractId;overwrite=parsed.Overwrite;}
            else if(verb=="Remove"){var parsed=MessagePackSerializer.Deserialize<RemoveFuturesOptionContractCommand>(payload,options);command=parsed;value=null;original=parsed.ContractId;overwrite=parsed.Overwrite;}
            else throw new InvalidDataException("Unknown option command");
            if(!commands.Add(command.CommandId))throw new InvalidDataException("Duplicate option submission");
            if(command.Subject.ToString()!=subject||subject!=$"Command.FuturesOptionContractCommand.{verb}.{command.EntityId.Format()}"||command.RouteTo!=BoundedContextName.FuturesOptionContractBoundedContext||!overwrite||(bool)command.GetType().GetProperty("PostEvents")!.GetValue(command)!)throw new InvalidDataException("Invalid option command identity/metadata");
            if(verb!="Add"&&!rows.ContainsKey(original))throw new InvalidDataException("Option contract no longer exists");
            if(value is not null){
                if(!value.IsValid||command.EntityId.ContractId!=value.ContractId||command.EntityId.Year!=value.ContractMonth.Year)throw new InvalidDataException("Option route must use instrument expiry year");
                _=value.GetExactStrikePrice();
                if(value.Description=="Reject")throw new InvalidDataException("Option fixture rejected mutation");
                if(value.ReviewState==ReferenceReviewState.Reviewed&&FuturesReferenceQualification.Errors(value).Count>0)throw new InvalidDataException("Option reference is not qualified");
                if(verb=="Change"&&rows[original].SchemaVersion>0){var before=rows[original];if(value.ContractId!=before.ContractId||value.Symbol!=before.Symbol||value.GetExactStrikePrice()!=before.GetExactStrikePrice()||value.DefinitionDigest!=before.DefinitionDigest||value.InstrumentId!=before.InstrumentId||value.UnderlyingContractId!=before.UnderlyingContractId)throw new InvalidDataException("Immutable option provider evidence changed");}
            }
            rows.Remove(original);if(value is not null)rows[value.ContractId]=value;version++;
            var now=new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc);var eventSubject=new ActorSubject(ActorType.Event,"FuturesOptionContractEvent",verb=="Add"?"AddedComplete":verb=="Change"?"ChangedComplete":"RemovedComplete",command.EntityId.Format());
            object terminal=verb switch{
                "Add"=>new FuturesOptionContractAddedCompleteEvent{Subject=eventSubject,EntityId=command.EntityId,CommandId=command.CommandId,Id=Guid.NewGuid(),ReceivedOn=now,Contract=value,CreatedOn=now,CreatedBy="Fixture"},
                "Change"=>new FuturesOptionContractChangedCompleteEvent{Subject=eventSubject,EntityId=command.EntityId,CommandId=command.CommandId,Id=Guid.NewGuid(),ReceivedOn=now,Contract=value,OriginalContractId=original,UpdatedOn=now,UpdatedBy="Fixture"},
                _=>new FuturesOptionContractRemovedCompleteEvent{Subject=eventSubject,EntityId=command.EntityId,CommandId=command.CommandId,Id=Guid.NewGuid(),ReceivedOn=now,ContractId=original,DeletedOn=now,DeletedBy="Fixture"}
            };
            if(value?.Description!="NoTerminal")await connection.PublishAsync(eventSubject.ToString(),MessagePackSerializer.Serialize(terminal.GetType(),terminal,options),cancellationToken:token);
            if(value?.Description=="SlowRefresh")Mode="HoldReads";
            return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=true,ErrorMessage="",Value=new(command.CommandId)},options);
        }catch(Exception error){return MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=6007,ErrorMessage=error.Message},options);}
    }
}

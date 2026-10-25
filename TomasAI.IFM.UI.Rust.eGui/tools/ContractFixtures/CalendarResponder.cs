using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

// In-memory protocol oracle only. Never registered with the Development API.
internal sealed class CalendarResponder
{
    readonly Dictionary<EconomicCalendarId, EconomicCalendarReadModel> rows = [];
    public CalendarResponder(){
        var row=new EconomicCalendarReadModel(new(2026,10,26,12,30,0,DateTimeKind.Utc),"CA","Sidebar Fixture","1.2","1.1","1.0",new(2026,10,26,12,30,0,DateTimeKind.Utc),"Fixture",null,null,null,null);
        rows.Add(row.Id,row);
    }
    readonly DateTime now = new(2026,10,26,12,30,0,DateTimeKind.Utc);
    static T Read<T>(byte[] bytes) => MessagePackSerializer.Deserialize<T>(bytes, MessagePackBinarySerializer.Options);
    static byte[] Success<T>(T value) => MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},MessagePackBinarySerializer.Options);
    static DateTime Start(DateTime date,EconomicCalendarViewType period)=>period switch {
        EconomicCalendarViewType.Today=>date,EconomicCalendarViewType.Yesterday=>date.AddDays(-1),EconomicCalendarViewType.Tomorrow=>date.AddDays(1),
        EconomicCalendarViewType.ThisWeek=>date.Date.AddDays(-(((int)date.DayOfWeek+6)%7)).Add(date.TimeOfDay),
        EconomicCalendarViewType.NextWeek=>date.Date.AddDays(7-(((int)date.DayOfWeek+6)%7)).Add(date.TimeOfDay),_=>throw new InvalidDataException("Invalid calendar period")
    };
    public async Task<byte[]> Handle(NatsConnection connection, string subject, byte[] bytes, CancellationToken cancellationToken)
    {
        if(subject == "Query.EconomicCalendarQuery.GetEconomicCalendarCountryCodes.EconomicCalendarCountryCodes")
        {
            var query = Read<GetEconomicCalendarCountryCodesQuery>(bytes);
            if(query.Subject.ToString()!=subject)throw new InvalidDataException("Country subject mismatch");
            return Success<EconomicCalendarCountryCodeReadModel[]>([new("US"),new("CA")]);
        }
        if(subject.StartsWith("Query.EconomicCalendarQuery.GetEconomicCalendarDate.",StringComparison.Ordinal)) {
            var query=Read<GetEconomicCalendarDateQuery>(bytes);
            var expected=new GetEconomicCalendarDateParameter(query.TodaysDate,query.CalendarViewType);
            if(query.Subject.ToString()!=subject || query.Subject.EntityId!=expected.Format())throw new InvalidDataException("Calendar date query mismatch");
            var day=Start(query.TodaysDate,query.CalendarViewType);
            return Success($"{day.DayOfWeek}, {day:MMMM} {day:dd}, {day:yyyy}");
        }
        if(subject.StartsWith("Query.",StringComparison.Ordinal))
        {
            var query=Read<GetEconomicCalendarQuery>(bytes);
            var expected=new GetEconomicCalendarParameter(query.TodaysDate,query.CalendarViewType,query.CountryCode);
            var zone=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            var local=TimeZoneInfo.ConvertTimeFromUtc(query.TodaysDate,zone);
            if(query.Subject.ToString()!=subject || query.Subject.EntityId!=expected.Format() || local.TimeOfDay!=TimeSpan.Zero)
                throw new InvalidDataException("Calendar range is not Eastern midnight or route mismatch");
            var start=Start(query.TodaysDate,query.CalendarViewType);var end=start.AddDays(query.CalendarViewType is EconomicCalendarViewType.ThisWeek or EconomicCalendarViewType.NextWeek?7:1);
            return Success(rows.Values.Where(r=>r.CountryCode==query.CountryCode && r.EventDate>=start && r.EventDate<end).OrderBy(r=>r.EventDate).ToArray());
        }
        try
        {
            var verb=subject.Split('.')[2];
            Guid commandId; EconomicCalendarId entity; string stem; EconomicCalendarReadModel? row=null;
            switch(verb)
            {
                case "Add":
                    var add=Read<AddEconomicCalendarCommand>(bytes); Validate(add.Subject,add.EntityId,add.RouteTo,add.ErrorCode,AddEconomicCalendarCommand.ErrorId);
                    commandId=add.CommandId;entity=add.EntityId;row=add.EconomicCalendar;stem="EconomicCalendarAdded";
                    if(entity!=row.Id || rows.ContainsKey(entity))throw new InvalidDataException("Invalid or duplicate event identity");
                    break;
                case "Change":
                    var change=Read<ChangeEconomicCalendarCommand>(bytes); Validate(change.Subject,change.EntityId,change.RouteTo,change.ErrorCode,ChangeEconomicCalendarCommand.ErrorId);
                    commandId=change.CommandId;entity=change.EntityId;row=change.EconomicCalendar;stem="EconomicCalendarChanged";
                    if(!change.Overwrite || change.EconomicCalendarId!=entity || !rows.ContainsKey(entity))throw new InvalidDataException("Invalid original identity");
                    break;
                case "Remove":
                    var remove=Read<RemoveEconomicCalendarCommand>(bytes); Validate(remove.Subject,remove.EntityId,remove.RouteTo,remove.ErrorCode,RemoveEconomicCalendarCommand.ErrorId);
                    commandId=remove.CommandId;entity=remove.EntityId;stem="EconomicCalendarRemoved";
                    if(!remove.Overwrite || remove.EconomicCalendarId!=entity || !rows.ContainsKey(entity))throw new InvalidDataException("Invalid original identity");
                    break;
                case "Import":
                    var import=Read<ImportEconomicCalendarsCommand>(bytes); Validate(import.Subject,import.EntityId,import.RouteTo,import.ErrorCode,ImportEconomicCalendarsCommand.ErrorId);
                    commandId=import.CommandId;entity=import.EntityId;stem="EconomicCalendarsImported";
                    var local=TimeZoneInfo.ConvertTimeFromUtc(import.ImportedDate,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
                    if(local.TimeOfDay!=TimeSpan.Zero || import.CountryCodes.Length!=1 || import.DuplicatePolicy!=ImportDuplicatePolicy.Overwrite || entity.CountryCode!="ZZ")throw new InvalidDataException("Invalid calendar import");
                    row=new(import.ImportedDate.AddHours(8),import.CountryCodes[0],"Offline Import","1.2","1.1","1.0",now,"Fixture","High","%",null,null);
                    break;
                default:throw new InvalidDataException("Unsupported calendar command");
            }
            if(row?.EventName=="Reject")throw new InvalidDataException("Fixture calendar rejection");
            rows.Remove(entity);if(row!=null)rows[row.Id]=row;
            var terminalType=typeof(AddEconomicCalendarCommand).Assembly.GetType($"TomasAI.IFM.Domain.MarketData.Shared.Events.{stem}CompleteEvent")!;
            var terminal=Activator.CreateInstance(terminalType)!;
            var terminalVerb=verb switch{"Add"=>"AddedComplete","Change"=>"ChangedComplete","Remove"=>"RemovedComplete",_=>"ImportedComplete"};
            var eventSubject=new ActorSubject(ActorType.Event,"EconomicCalendarEvent",terminalVerb,entity.Format());
            foreach(var (name,value) in new (string,object)[]{("Subject",eventSubject),("EntityId",entity),("CommandId",commandId),("Id",Guid.NewGuid()),("ReceivedOn",now)})terminalType.GetProperty(name)!.SetValue(terminal,value);
            // An event can arrive before the request acknowledgement.
            if(row?.EventName!="NoTerminal")await connection.PublishAsync(eventSubject.ToString(),MessagePackSerializer.Serialize(terminalType,terminal,MessagePackBinarySerializer.Options),cancellationToken:cancellationToken);
            if(row?.EventName=="MalformedAck")return MessagePackSerializer.Serialize(42,MessagePackBinarySerializer.Options);
            return Success(new ScalarReadModel<Guid>(commandId));
        }
        catch(Exception error)
        {
            return MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>>{Success=false,ErrorCode=10003,ErrorMessage=error.Message},MessagePackBinarySerializer.Options);
        }
        void Validate(ActorSubject route,EconomicCalendarId entity,BoundedContextName context,int error,int expected)
        {
            if(route.ToString()!=subject || route.EntityId!=entity.Format() || context!=BoundedContextName.EconomicCalendarBoundedContext || error!=expected)throw new InvalidDataException("Invalid calendar envelope");
        }
    }
}

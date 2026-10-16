using System.Globalization;
using System.Threading.Channels;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;
[MessagePack.MessagePackObject]
public sealed record CalendarDisplayRow([property: MessagePack.Key("id")] string Id, [property: MessagePack.Key("time")] string Time, [property: MessagePack.Key("country")] string Country, [property: MessagePack.Key("event")] string Event, [property: MessagePack.Key("actual")] string Actual, [property: MessagePack.Key("forecast")] string Forecast, [property: MessagePack.Key("prior")] string Prior);
[MessagePack.MessagePackObject]
public sealed record StatusDisplayRow([property: MessagePack.Key("id")] string Id, [property: MessagePack.Key("time")] string Time, [property: MessagePack.Key("message")] string Message);
[MessagePack.MessagePackObject]
public sealed record SidebarPublication([property: MessagePack.Key("period")] string Period, [property: MessagePack.Key("country")] string Country, [property: MessagePack.Key("countries")] string[] Countries, [property: MessagePack.Key("date")] string Date, [property: MessagePack.Key("calendarRows")] CalendarDisplayRow[] CalendarRows, [property: MessagePack.Key("selectedId")] string SelectedId, [property: MessagePack.Key("statusRows")] StatusDisplayRow[] StatusRows, [property: MessagePack.Key("busy")] bool Busy, [property: MessagePack.Key("error")] string Error, [property: MessagePack.Key("errorSequence")] long ErrorSequence)
{ [MessagePack.Key("feedControl")] public ShellFeedControl FeedControl {get;init;} = new(false,false,false,false); }
/// <summary>Owns the unchanged WinForms calendar and shell view models; publishes their live console and calendar snapshots.</summary>
sealed class SidebarSession : IDisposable
{
    readonly CancellationTokenSource stopping=new();
    readonly Channel<uint> actions=Channel.CreateBounded<uint>(64);
    readonly object gate=new();
    readonly List<StatusConsoleLogReadModel> logs=[];
    readonly Task worker;
    readonly CoalescedDisplayPublisher displayPublisher;
    Task feedCommand=Task.CompletedTask;
    bool feedPending;
    public const uint FeedToggleAction=0xB0000000u;
    MarketEconomicCalendarViewModel? calendar;
    RustShellViewModelSession? shell;
    string period="Today";bool busy;string error="";long errorSequence;
    byte[] publication=BridgeWire.Serialize(new SidebarPublication("Today","",[],"",[],"",[],false,"",0));
    public byte[] Publication=>Volatile.Read(ref publication);
    public SidebarSession(){
        displayPublisher=new(PublishNow,e=>Console.Error.WriteLine(e));
        worker=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")=="live"?Task.Run(RunAsync):Task.CompletedTask;
    }
    public bool Select(uint action) {
        if(action==FeedToggleAction) {
            lock(gate) {
                if(stopping.IsCancellationRequested || feedPending || shell?.FeedControl.CanToggle!=true)return true;
                feedPending=true;
                feedCommand=Task.Run(async()=>{
                    try {await shell.ToggleMarketDataFeedAsync(stopping.Token);}
                    catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
                    catch(Exception error){RecordError(error);}
                    finally {lock(gate)feedPending=false;Publish();}
                });
            }
            Publish();return true;
        }
        if((action&0xF0000000u) is not (0x80000000u or 0x90000000u or 0xA0000000u))return false;
        actions.Writer.TryWrite(action);return true;
    }
    async Task RunAsync()
    {
        await using var scope=new RustClientServiceScope(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");
        try
        {
            await scope.StartAsync(stopping.Token);
            shell=new RustShellViewModelSession(scope, RecordError, snapshot => {
                lock(gate) { logs.Clear(); logs.AddRange(snapshot); }
                Publish();
            }, Publish);
            calendar=new(scope,scope.Container.GetInstance<IEconomicCalendarService>());
            calendar.PropertyChanged+=(_,_)=>Publish();
            busy=true;Publish();
            await calendar.InitializeAsync(stopping.Token);
            await calendar.LoadCountryCodesOperation.ExecuteAsync(stopping.Token);
            await RefreshAsync();
            await foreach(var action in actions.Reader.ReadAllAsync(stopping.Token))
            {
                try
                {
                    var index=(int)(action&0x0FFFFFFFu);
                    switch(action&0xF0000000u)
                    {
                        case 0x80000000u:
                            if(index>=5)break;
                            period=new[]{"Today","Yesterday","Tomorrow","This Week","Next Week"}[index];await RefreshAsync();break;
                        case 0x90000000u:
                            if(calendar.SelectCountryCode(index))await RefreshAsync();break;
                        case 0xA0000000u:
                            calendar.SelectEconomicCalendar(index);Publish();break;
                    }
                }
                catch(Exception e)when(e is not OperationCanceledException){RecordError(e);}
            }
        }
        catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
        catch(Exception e){RecordError(e);}
        finally
        {
            Task pending; lock(gate)pending=feedCommand; await pending;
            if(shell is not null)await shell.DisposeAsync();
            if(calendar is not null)await calendar.DisposeAsync();
        }
    }
    async Task RefreshAsync()
    {
        busy=true;Publish();
        try {calendar!.SelectCalendarPeriod(period,EasternTime.GetNow(TimeProvider.System));await calendar.RefreshOperation.ExecuteAsync(stopping.Token);}
        finally {busy=false;Publish();}
    }
    void RecordError(Exception e){lock(gate){error=e.GetBaseException().Message;errorSequence++;busy=false;}Publish();}
    void Publish()=>displayPublisher.Request();
    void PublishNow()
    {
        StatusConsoleLogReadModel[] capturedLogs;
        string capturedPeriod,capturedError;bool capturedBusy,capturedFeedPending;long capturedErrorSequence;
        lock(gate){capturedLogs=logs.ToArray();capturedPeriod=period;capturedError=error;capturedBusy=busy;capturedFeedPending=feedPending;capturedErrorSequence=errorSequence;}
            var current=calendar;
            var rows=current?.EconomicCalendars.Select(row=>new CalendarDisplayRow($"{row.EventDate:O}|{row.CountryCode}|{row.EventName}",EasternTime.FromUtc(row.EventDate).ToString("t",CultureInfo.CurrentCulture),row.CountryCode,row.EventName,row.Actual??"",row.Forecast??"",row.Prior??"")).ToArray()??[];
            var selected=current?.SelectedEconomicCalendar;
            var modelError=current?.LastError;
            var snapshot=new SidebarPublication(capturedPeriod,current?.SelectedCountryCode??"",current?.CountryCodes.ToArray()??[],current?.CalendarDate??"",rows,selected is null?"":$"{selected.EventDate:O}|{selected.CountryCode}|{selected.EventName}",capturedLogs.Select(log=>new StatusDisplayRow($"{log.StatusDate:O}|{log.Message}",EasternTime.FromUtc(log.StatusDate).ToString("hh:mm:ss.fff tt",CultureInfo.InvariantCulture),log.Message)).ToArray(),capturedBusy,modelError?.Message??capturedError,Math.Max(modelError?.Sequence??0,capturedErrorSequence)) { FeedControl = shell?.FeedControl is {} feed ? feed with { Busy=feed.Busy||capturedFeedPending, CanToggle=feed.CanToggle&&!capturedFeedPending } : new(false,false,capturedFeedPending,false) };
            Volatile.Write(ref publication,BridgeWire.Serialize(snapshot));
    }
    public void Dispose(){displayPublisher.DisposeAsync().AsTask().GetAwaiter().GetResult();stopping.Cancel();actions.Writer.TryComplete();worker.GetAwaiter().GetResult();stopping.Dispose();}
}

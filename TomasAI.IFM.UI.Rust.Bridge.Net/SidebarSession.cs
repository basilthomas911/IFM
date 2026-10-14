using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed record CalendarDisplayRow(string Id,string Time,string Country,string Event,string Actual,string Forecast,string Prior);
public sealed record StatusDisplayRow(string Id,string Time,string Message);
public sealed record SidebarPublication(string Period,string Country,string[] Countries,string Date,CalendarDisplayRow[] CalendarRows,string SelectedId,StatusDisplayRow[] StatusRows,bool Busy,string Error,long ErrorSequence);
/// <summary>Owns the unchanged WinForms calendar and shell view models; publishes their live console and calendar snapshots.</summary>
sealed class SidebarSession : IDisposable
{
    readonly CancellationTokenSource stopping=new();
    readonly Channel<uint> actions=Channel.CreateBounded<uint>(64);
    readonly object gate=new();
    readonly List<StatusConsoleLogReadModel> logs=[];
    readonly Task worker;
    MarketEconomicCalendarViewModel? calendar;
    RustShellViewModelSession? shell;
    string period="Today";bool busy;string error="";long errorSequence;
    byte[] publication=JsonSerializer.SerializeToUtf8Bytes(new SidebarPublication("Today","",[],"",[],"",[],false,"",0),JsonOptions);
    static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public byte[] Publication=>Volatile.Read(ref publication);
    public SidebarSession()=>worker=Environment.GetEnvironmentVariable("IFM_RUST_UI_MODE")=="live"?Task.Run(RunAsync):Task.CompletedTask;
    public bool Select(uint action) {
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
            });
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
    void Publish()
    {
        lock(gate)
        {
            var current=calendar;
            var rows=current?.EconomicCalendars.Select(row=>new CalendarDisplayRow($"{row.EventDate:O}|{row.CountryCode}|{row.EventName}",EasternTime.FromUtc(row.EventDate).ToString("t",CultureInfo.CurrentCulture),row.CountryCode,row.EventName,row.Actual??"",row.Forecast??"",row.Prior??"")).ToArray()??[];
            var selected=current?.SelectedEconomicCalendar;
            var modelError=current?.LastError;
            var snapshot=new SidebarPublication(period,current?.SelectedCountryCode??"",current?.CountryCodes.ToArray()??[],current?.CalendarDate??"",rows,selected is null?"":$"{selected.EventDate:O}|{selected.CountryCode}|{selected.EventName}",logs.Select(log=>new StatusDisplayRow($"{log.StatusDate:O}|{log.Message}",EasternTime.FromUtc(log.StatusDate).ToString("hh:mm:ss.fff tt",CultureInfo.InvariantCulture),log.Message)).ToArray(),busy,modelError?.Message??error,Math.Max(modelError?.Sequence??0,errorSequence));
            Volatile.Write(ref publication,JsonSerializer.SerializeToUtf8Bytes(snapshot,JsonOptions));
        }
    }
    public void Dispose(){stopping.Cancel();actions.Writer.TryComplete();worker.GetAwaiter().GetResult();stopping.Dispose();}
}

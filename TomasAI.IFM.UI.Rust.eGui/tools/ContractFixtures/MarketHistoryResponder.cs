using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>Isolated protocol oracle: rejects machine-date or unbounded history queries.</summary>
internal static class MarketHistoryResponder
{
    public static byte[]? Handle(string subject,byte[] payload,string directory,bool outlookAvailable)
    {
        var options=MessagePackBinarySerializer.Options;
        var date=new DateOnly(2026,10,26);var end=new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc);
        byte[] Result<T>(T value)=>MessagePackSerializer.Serialize(new ServiceResult<T>{Success=true,ErrorMessage="",Value=value},options);
        if(subject.StartsWith("Query.FuturesContractQuery.GetOnTheRunFuturesContract.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetOnTheRunFuturesContractQuery>(payload,options);
            if(query.Subject.ToString()!=subject||query.Symbol is not ("ES" or "VX"))throw new InvalidDataException("Invalid current-contract route");
            return Result(new FuturesContractV3ReadModel{ContractId=query.Symbol+"Z6",Symbol=query.Symbol,Description="Fixture",LocalSymbol=query.Symbol+"Z6",SecurityType="FUT",Currency="USD",Exchange="Fixture",Multiplier="50",LastTradeDate=date.AddMonths(2),OnTheRun=true});
        }
        if(subject.StartsWith("Query.FuturesBarDataQuery.GetFuturesBarData.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetFuturesBarDataQuery>(payload,options);
            // The unchanged hybrid uses wall UTC for its chart window. Preserve
            // that request in desktop evidence; native protocol gates stay exact.
            var validWindow=query.EndDate==end&&query.StartDate==end.AddHours(-6)||DesktopFixtureMode.Enabled&&Math.Abs((query.EndDate-DateTime.UtcNow).TotalHours)<=5&&query.EndDate-query.StartDate==TimeSpan.FromHours(6);
            if(query.Subject.ToString()!=subject||subject!="Query.FuturesBarDataQuery.GetFuturesBarData."+query.EntityId.Format()||query.ValueDate!=date||!validWindow||!query.AcrossValueDates||query.ContractId!=query.Symbol+"Z6")throw new InvalidDataException("History must use the API clock, contract and six-hour cross-date window: "+MessagePackSerializer.ConvertToJson(payload,options));
            return Result(new[]{new FuturesBarDataReadModel(query.ContractId,query.Symbol,date,end.AddMinutes(-1),BarRateType.FifteenSeconds,6050.125m,6100,6000)});
        }
        if(subject.StartsWith("Query.MarketOutlookSnapshotQuery.GetFuturesBollingerBandHistory.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetFuturesBollingerBandHistoryQuery>(payload,options);
            if(query.Subject.ToString()!=subject||query.ValueDate!=date||query.RootSymbol!="ES"||!(query.MaxDays==40||DesktopFixtureMode.Enabled&&query.MaxDays==39))throw new InvalidDataException("Invalid bounded Daily history: "+MessagePackSerializer.ConvertToJson(payload,options));
            return Result(new[]{new FuturesBbSignalReadModel{Metadata=new(){ValueDate=date,IsValid=true,SignalKey=new(){TimeFrame=TimeFrameType.Daily},MarketDataAsOfUtc=new(end)},Price=6050.125m,Ema20Center=6040,Upper20=6060,Lower20=6020}});
        }
        if(subject.StartsWith("Query.MarketOutlookSnapshotQuery.GetMarketOutlookSnapshot.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetMarketOutlookSnapshotQuery>(payload,options);
            if(query.Subject.ToString()!=subject||query.ValueDate!=date||query.ContractId!="ESZ6"||!subject.EndsWith(".ESZ6.20261026",StringComparison.Ordinal))throw new InvalidDataException("Invalid API-owned outlook date");
            if(outlookAvailable)return Result(MessagePackSerializer.Deserialize<MarketOutlookReadModel>(File.ReadAllBytes(Path.Combine(directory,"outlook-1-True.msgpack")),options));
            return MessagePackSerializer.Serialize(new ServiceResult<MarketOutlookReadModel>{Success=false,ErrorCode=1019,ErrorMessage="Optional current preview unavailable"},options);
        }
        return null;
    }
}

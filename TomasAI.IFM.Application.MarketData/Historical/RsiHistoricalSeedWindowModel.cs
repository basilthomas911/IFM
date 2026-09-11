using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Application.MarketData.Historical;
/// <summary>Completed session-aligned RSI intervals, including previous trading sessions and Daily closes.</summary>
public static class RsiHistoricalSeedWindowModel
{
 public static MarketSessionBounds[] Create(TimeFrameType interval,int count,DateTimeOffset asOfUtc,IMarketSessionCalendar calendar)
 {
  if(count is <1 or >512||asOfUtc==default||asOfUtc.Offset!=TimeSpan.Zero)throw new ArgumentException("RSI.SEED_REQUEST_INVALID");
  var duration=interval switch {TimeFrameType.FifteenSeconds=>TimeSpan.FromSeconds(15),TimeFrameType.OneMinute=>TimeSpan.FromMinutes(1),
   TimeFrameType.FiveMinutes=>TimeSpan.FromMinutes(5),TimeFrameType.FifteenMinutes=>TimeSpan.FromMinutes(15),TimeFrameType.OneHour=>TimeSpan.FromHours(1),
   TimeFrameType.FourHours=>TimeSpan.FromHours(4),TimeFrameType.Daily=>TimeSpan.FromDays(1),_=>throw new ArgumentException("RSI.SEED_INTERVAL_UNSUPPORTED")};
  var result=new List<MarketSessionBounds>();var date=DateOnly.FromDateTime(asOfUtc.UtcDateTime).AddDays(1);
  for(var lookback=0;lookback<1024&&result.Count<count;lookback++,date=date.AddDays(-1))
  {
   if(!calendar.IsTradingDate(date))continue;
   var session=calendar.GetSession(date);
   if(interval==TimeFrameType.Daily){if(session.EndUtc<=asOfUtc)result.Add(session);continue;}
   var intervals=(int)Math.Ceiling((session.EndUtc-session.StartUtc)/duration);
   for(var index=intervals-1;index>=0&&result.Count<count;index--)
   {
    var start=session.StartUtc+index*duration;var end=start+duration;if(end>session.EndUtc)end=session.EndUtc;
    if(end<=asOfUtc)result.Add(new(date,start,end));
   }
  }
  if(result.Count!=count)return [];
  result.Reverse();return result.ToArray();
 }
}

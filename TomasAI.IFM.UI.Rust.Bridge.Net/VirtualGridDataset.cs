using System.Globalization;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed record GridRow(long Id,long Sequence,string Timestamp,string DailyPnl,string ForwardLossRatio);
/// <summary>Deterministic display dataset; never writes application storage.</summary>
public static class VirtualGridDataset
{
 public static GridRow[] Create(int count=100000)
 {
  if(count<0||count>100000)throw new ArgumentOutOfRangeException(nameof(count));
  var start=new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc);
  return Enumerable.Range(1,count).Select(i=>new GridRow(i,i,start.AddSeconds(i).ToString("O",CultureInfo.InvariantCulture),((i%2000-1000)/10m).ToString("F2",CultureInfo.InvariantCulture),((i%100)/100m).ToString("F2",CultureInfo.InvariantCulture))).ToArray();
 }
}

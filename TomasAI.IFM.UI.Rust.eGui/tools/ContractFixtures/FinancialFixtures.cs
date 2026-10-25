using MessagePack;
using System.Text.Json;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
/// <summary>Server-report formatting oracle. Fixture numbers are never calculated as live trading metrics.</summary>
internal static class FinancialFixtures {
 public static readonly FundPerformanceMetrics Report=new(){HasHistory=true,WinRate=0.625m,AverageProfit=12345.675m,LossRate=0.375m,AverageLoss=234.565m,WinLossRatio=2.125m,ActualSharpeRatio=-1.2345m,PnlAmount=-1234567.895m,PnlPercent=-0.1234567m,TradeCommission=123.455m,MaximumDrawdownPercent=0.23456m,From=new(2026,1,1),Through=new(2026,10,26),CalculationBasis="Fixture report basis: metrics are calculated by the backend."};
 public static void Export(string output){
  FixtureFiles.WriteAllText(Path.Combine(output,"financial-short-dates.json"),System.Text.Json.JsonSerializer.Serialize(new[]{new DateOnly(2026,1,1),new DateOnly(2026,10,26),new DateOnly(2027,12,31)}.Select(d=>new{Date=d.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),Display=d.ToString("d",System.Globalization.CultureInfo.CurrentCulture)})));
  var cases=new List<object>();
  foreach(var (report,index) in new[]{Report,Report with{MaximumDrawdownPercent=null},Report with{HasHistory=false}}.Select((v,i)=>(v,i))){
   var fields=new Dictionary<string,string>();
   if(report.HasHistory){fields["metricsBasis"]=report.CalculationBasis;string[] values=[report.WinRate.ToString("P2"),report.AverageProfit.ToString("N2"),report.LossRate.ToString("P2"),report.AverageLoss.ToString("N2"),report.WinLossRatio.ToString("F2"),report.ActualSharpeRatio.ToString("F2"),report.PnlAmount.ToString("N2"),report.PnlPercent.ToString("P2"),report.TradeCommission.ToString("N2"),report.MaximumDrawdownPercent?.ToString("P2")??"N/A"];for(var i=0;i<values.Length;i++)fields[$"metric{i}"]=values[i];}
   var name=$"financial-metrics-{index}";FixtureFiles.WriteAllBytes(Path.Combine(output,name+".msgpack"),MessagePackSerializer.Serialize(report,MessagePackBinarySerializer.Options));cases.Add(new{name,fields});
  }
  FixtureFiles.WriteAllText(Path.Combine(output,"financial-metrics-display.json"),JsonSerializer.Serialize(cases));
 }
}

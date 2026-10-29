using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Copies raw EOD and indicator facts for native strike-window policy.
/// No eligibility, fallback, decimal conversion, clock/date inference or native
/// source resource is owned here. Rust may abandon this I/O independently.</summary>
public sealed class NativeBrowserFacts(IDbContextFactory databases)
{
    /// <summary>Returns all raw fallback candidates; Rust alone selects them.</summary>
    public async Task<byte[]> ReadAsync(JsonElement input,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var contract=input.GetProperty("contract").GetString();var root=input.GetProperty("root").GetString();
        var date=DateOnly.FromDayNumber(input.GetProperty("date").GetInt32());
        if(string.IsNullOrWhiteSpace(contract)||contract.Length>1024||string.IsNullOrWhiteSpace(root)||root.Length>60||date==default)
            throw new InvalidDataException("Invalid raw browser input identity.");
        var db=databases.MarketDataDb;
        var current=await db.GetFuturesEodDataAsync(contract,date).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var previous=await db.GetLastFuturesEodDataAsync(contract,date).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var series=MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId(root,"calendar-front","unadjusted",1));
        var band=await db.GetLatestFuturesBollingerBandSignalAsync(series,date,token).ConfigureAwait(false);
        var intraday=await db.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series,date,TimeFrameType.FiveMinutes,token).ConfigureAwait(false);
        static string Bits(double value)=>BitConverter.DoubleToUInt64Bits(value).ToString("X16",CultureInfo.InvariantCulture);
        return JsonSerializer.SerializeToUtf8Bytes(new object?[]{
            current is null?null:new object[]{current.ValueDate.DayNumber,current.ClosePrice.ToString(CultureInfo.InvariantCulture),Bits(current.DailyStdDevAmount)},
            previous is null?null:new object[]{previous.ValueDate.DayNumber,previous.ClosePrice.ToString(CultureInfo.InvariantCulture),Bits(previous.DailyStdDevAmount)},
            band is null?null:new object?[]{band.Price.ToString(CultureInfo.InvariantCulture),band.StandardDeviation20?.ToString(CultureInfo.InvariantCulture),band.Metadata.IsValid,band.Metadata.ValueDate.DayNumber},
            intraday is null?null:new object?[]{intraday.Price.ToString(CultureInfo.InvariantCulture),intraday.StandardDeviation20?.ToString(CultureInfo.InvariantCulture),intraday.Metadata.IsValid,intraday.Metadata.ValueDate.DayNumber}});
    }
}

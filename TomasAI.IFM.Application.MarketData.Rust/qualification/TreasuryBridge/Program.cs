using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Contracts;
var library=Path.GetFullPath(args[0]);using var oracle=JsonDocument.Parse(await File.ReadAllBytesAsync(args[1]));
var at=new DateTimeOffset(2026,9,8,16,0,0,TimeSpan.Zero);var budget=TimeSpan.FromSeconds(5);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var endpoint=$"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/treasury.xml";
using var stopping=new CancellationTokenSource();var peers=new ConcurrentBag<Task>();var entered=Channel.CreateBounded<int>(1024);var closed=Channel.CreateBounded<int>(1024);
string? response="";int requests=0;
var server=Task.Run(async()=>{try{while(true){var peer=await listener.AcceptTcpClientAsync(stopping.Token);peers.Add(Task.Run(async()=>{using(peer){
    try{var stream=peer.GetStream();using var reader=new StreamReader(stream,Encoding.UTF8,false,1024,true);var line=await reader.ReadLineAsync(stopping.Token);
        if(line is null)return;if(!line.StartsWith("GET /treasury.xml?data=daily_treasury_yield_curve&field_tdr_date_value_month=202609 "))throw new InvalidDataException("Wrong reference query: "+line);
        while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync(stopping.Token))){if(line.StartsWith("Authorization:",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Provider credential leaked to official reference source.");}
        entered.Writer.TryWrite(Interlocked.Increment(ref requests));var xml=Volatile.Read(ref response);
        if(xml is not null){var bytes=Encoding.UTF8.GetBytes(xml);await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length}\r\nX-Warning: treasury warning\r\nX-Request-Id: treasury-ffi\r\nConnection: close\r\n\r\n"),stopping.Token);await stream.WriteAsync(bytes,stopping.Token);}
        if(await reader.ReadLineAsync(stopping.Token)is not null)throw new InvalidDataException("Expected reference connection cleanup.");
    }catch(IOException){}finally{closed.Writer.TryWrite(1);}
}}));}}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}});
await using var runtime=new NativeMarketDataRuntime(library,2);runtime.ConfigureLive(new(2026,9,8),Guid.NewGuid(),at,false,8);
runtime.ConfigureTreasury(endpoint,["127.0.0.1:1"]);var native=new NativeTreasuryCurve(runtime,budget);
using var http=new HttpClient();using var original=new UsTreasuryCurve(http);
void Equal<T>(T expected,T actual,string name){if(!MessagePackBinarySerializer.Shared.Serialize(expected)!.SequenceEqual(MessagePackBinarySerializer.Shared.Serialize(actual)!))throw new InvalidDataException(name+": expected "+expected+"; actual "+actual);}
var cases=0;TreasuryCurveSnapshot? last=null;
try{
    foreach(var item in oracle.RootElement.EnumerateArray()){
        Volatile.Write(ref response,item.GetProperty("xml").GetString());var now=at.AddSeconds(cases*61);runtime.UpdateLiveClock(now,false);cases++;
        TreasuryCurveSnapshot[]? rows=null;Exception? failed=null;
        try{rows=(await native.GetRangeAsync(new(2026,9,1),new(2026,9,30))).ToArray();}catch(InvalidOperationException error){failed=error;}
        if((rows is not null)!=item.GetProperty("valid").GetBoolean())throw new InvalidDataException(item.GetProperty("name").GetString()+": "+failed);
        await entered.Reader.ReadAsync().AsTask().WaitAsync(budget);await closed.Reader.ReadAsync().AsTask().WaitAsync(budget);
        var warnings=runtime.DrainHistoricalWarnings();if(warnings.Length!=1||warnings[0].Text!="treasury warning"||warnings[0].RequestId!="treasury-ffi")throw new InvalidDataException("Reference warning was lost or altered.");
        if(rows is null)continue;
        var expected=item.GetProperty("rows");if(expected.GetArrayLength()!=rows.Length)throw new InvalidDataException("Treasury row count differs.");
        for(var i=0;i<rows.Length;i++){
            var row=rows[i];var expectedRow=expected[i];if(row.ValueDate.ToString("yyyy-MM-dd")!=expectedRow.GetProperty("date").GetString()||row.RetrievedAtUtc!=now||row.Source!="USTreasury")throw new InvalidDataException("Reference identity/time differs.");
            if(row.Rates.Count!=expectedRow.GetProperty("rates").GetArrayLength())throw new InvalidDataException("Missing/extra source tenor.");
            for(var k=0;k<row.Rates.Count;k++){var point=expectedRow.GetProperty("rates")[k];if((int)row.Rates[k].Tenor!=point.GetProperty("months").GetInt32()||!decimal.GetBits(row.Rates[k].RatePercent).Select(x=>unchecked((uint)x)).SequenceEqual(point.GetProperty("bits").EnumerateArray().Select(x=>x.GetUInt32())))throw new InvalidDataException("Exact decimal source bits differ.");}
            foreach(var tenor in new[]{TreasuryTenor.OneMonth,TreasuryTenor.TwoMonth,TreasuryTenor.ThreeMonth,TreasuryTenor.SixMonth})
                Equal(original.GetContinuouslyCompoundedAnnualRate(row,tenor,UsTreasuryCurve.ConversionPolicy),native.GetContinuouslyCompoundedAnnualRate(row,tenor,UsTreasuryCurve.ConversionPolicy),"complete source conversion/digest");
            last=row;
        }
    }
    if(last is null)throw new InvalidDataException("No successful reference curve tested.");
    foreach(var bad in new[]{last with{Source="wrong"},last with{CountryCode="XX"},last with{CurrencyCode="EUR"},last with{ValueDate=default},last with{RetrievedAtUtc=at.ToOffset(TimeSpan.FromHours(1))},
        last with{Rates=[]},last with{Rates=[new(TreasuryTenor.OneMonth,5),new(TreasuryTenor.OneMonth,6)]},last with{Rates=[new((TreasuryTenor)99,5)]},last with{Rates=[new(TreasuryTenor.OneMonth,-200)]}})
        Equal(original.GetContinuouslyCompoundedAnnualRate(bad,TreasuryTenor.OneMonth,UsTreasuryCurve.ConversionPolicy),native.GetContinuouslyCompoundedAnnualRate(bad,TreasuryTenor.OneMonth,UsTreasuryCurve.ConversionPolicy),"invalid conversion precedence");
    Volatile.Write(ref response,null);runtime.UpdateLiveClock(at.AddDays(1),false);
    using(var cancel=new CancellationTokenSource()){
        var pending=native.GetRangeAsync(new(2026,9,1),new(2026,9,30),cancel.Token);await entered.Reader.ReadAsync().AsTask().WaitAsync(budget);cancel.Cancel();
        try{await pending;throw new InvalidDataException("Canceled reference query succeeded.");}catch(OperationCanceledException){}
        await closed.Reader.ReadAsync().AsTask().WaitAsync(budget);
    }
    Volatile.Write(ref response,oracle.RootElement[3].GetProperty("xml").GetString());
    if((await native.GetRangeAsync(new(2026,9,1),new(2026,9,30))).Count!=1)throw new InvalidDataException("Fresh reference retry failed.");
    await entered.Reader.ReadAsync().AsTask().WaitAsync(budget);await closed.Reader.ReadAsync().AsTask().WaitAsync(budget);
    await runtime.StopAndDisposeAsync(budget);await Task.WhenAll(peers).WaitAsync(budget);
    Console.WriteLine($"PASS: {cases} unchanged C# Treasury source cases through SDK-free native HTTP -> DLL -> existing service DTOs; exact decimal bits/full conversion digests; warnings, cancellation/EOF, fresh retry and released result ownership.");
}finally{stopping.Cancel();listener.Stop();await server;try{await Task.WhenAll(peers);}catch(OperationCanceledException)when(stopping.IsCancellationRequested){}}

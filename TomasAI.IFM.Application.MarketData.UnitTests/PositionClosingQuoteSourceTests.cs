using System.Net;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Recovery;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class PositionClosingQuoteSourceTests
{
    static readonly DateOnly Date=new(2026,11,17);
    static readonly DateTime Boundary=new(2026,11,17,22,0,0,DateTimeKind.Utc);
    const string Header="ts_recv,ts_event,instrument_id,sequence,bid_px_00,ask_px_00,bid_sz_00,ask_sz_00";
    static string Row(int seconds,int instrument=42,string bid="13000000000",string ask="15000000000",int size=1)
    {
        var ns=(Boundary.AddSeconds(seconds).Ticks-DateTime.UnixEpoch.Ticks)*100;
        return $"{ns},{ns},{instrument},123,{bid},{ask},{size},1";
    }
    [Fact]
    public async Task Selects_latest_eligible_original_record_with_exact_prices_timestamps_and_hashes()
    {
        var selected=Row(-1);
        var handler=new Handler("0",Header+"\n"+Row(-50)+"\n"+selected+"\n"+Row(1)+"\n");
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(handler),new(),()=>"test-key");
        var acquired=await provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default);
        Assert.NotNull(acquired);Assert.Equal(14,acquired.Quote.MarkPrice);
        Assert.Equal(Boundary.AddSeconds(-1),acquired.Quote.ObservedAtUtc);
        Assert.Equal(Header+"\n"+selected+"\n",Encoding.UTF8.GetString(acquired.OriginalRecord));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(acquired.OriginalRecord)),acquired.Quote.SourceArchiveSha256);
        Assert.Equal(3,handler.Calls);Assert.DoesNotContain("test-key",acquired.Quote.ToString());
    }
    [Theory]
    [InlineData("0.01")]
    [InlineData("invalid")]
    public async Task Unapproved_or_unverifiable_cost_prevents_the_quote_download(string cost)
    {
        var handler=new Handler(cost,Header+"\n"+Row(-1));
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(handler),new(),()=>"test-key");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default));
        Assert.Equal(2,handler.Calls);
    }
    [Theory]
    [InlineData("empty")]
    [InlineData("crossed")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("subtickfuture")]
    public async Task Unavailable_quotes_do_not_create_evidence(string shape)
    {
        var row=shape switch {"empty"=>"","crossed"=>Row(-1,bid:"16000000000"),"stale"=>Row(-901),"subtickfuture"=>Row(0).Replace(((Boundary.Ticks-DateTime.UnixEpoch.Ticks)*100).ToString(),((Boundary.Ticks-DateTime.UnixEpoch.Ticks)*100+1).ToString(),StringComparison.Ordinal),_=>Row(1)};
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(new Handler("0",Header+"\n"+row)),new(),()=>"test-key");
        Assert.Null(await provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Absent_bid_is_explicit_for_options_and_rejected_for_futures(bool option)
    {
        var handler=new Handler("0",Header+"\n"+Row(-1,bid:"9223372036854775807",size:0));
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(handler),new(),()=>"test-key");
        var result=await provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3",option),Date,Boundary,default);
        if(option){Assert.NotNull(result);Assert.False(result.Quote.HasBid);Assert.Equal(15,result.Quote.MarkPrice);}
        else Assert.Null(result);
    }
    [Fact]
    public async Task Wrong_instrument_cannot_be_attributed_to_the_requested_contract()
    {
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(new Handler("0",Header+"\n"+Row(-1,instrument:43))),new(),()=>"test-key");
        await Assert.ThrowsAsync<InvalidDataException>(()=>provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default));
    }
    [Theory]
    [InlineData("2026-11-17T22:00:00.000000000Z")]
    [InlineData("2026-11-17T22:00:14.999999999Z")]
    public async Task Cost_and_download_stop_at_the_schema_watermark_without_rounding_nanoseconds(string available)
    {
        var handler=new Handler("0",Header+"\n"+Row(-1),available);
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(handler),new(),()=>"test-key");
        Assert.NotNull(await provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default));
        Assert.Equal(2,handler.Forms.Count);
        Assert.All(handler.Forms,form=>Assert.Contains("end="+Uri.EscapeDataString(available),form,StringComparison.Ordinal));
    }
    [Fact]
    public async Task Delayed_availability_widens_the_window_but_never_accepts_a_stale_close()
    {
        var handler=new Handler("0",Header+"\n"+Row(-120),"2026-11-17T21:58:30Z");
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(handler),new(),()=>"test-key");
        var result=await provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default);
        Assert.NotNull(result);Assert.Equal(Boundary.AddSeconds(-120),result.Quote.ObservedAtUtc);
        Assert.All(handler.Forms,form=>Assert.Contains("start="+Uri.EscapeDataString(Boundary.AddMinutes(-5).ToString("O")),form,StringComparison.Ordinal));
    }
    [Fact]
    public async Task Availability_older_than_the_freshness_window_does_not_request_or_invent_quotes()
    {
        var handler=new Handler("0",Header+"\n"+Row(-901),"2026-11-17T21:45:00Z");
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(handler),new(),()=>"test-key");
        Assert.Null(await provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default));
        Assert.Equal(1,handler.Calls);Assert.Empty(handler.Forms);
    }
    [Fact]
    public async Task Provider_errors_retain_the_operation_and_case_without_credentials()
    {
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(new ErrorHandler()),new(),()=>"test-key");
        var error=await Assert.ThrowsAsync<HttpRequestException>(()=>provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3"),Date,Boundary,default));
        Assert.Contains("metadata.get_dataset_range:422:data_end_after_available_end",error.Message,StringComparison.Ordinal);
        Assert.DoesNotContain("test-key",error.Message,StringComparison.Ordinal);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Complete_closing_book_uses_exact_raw_symbol_and_original_time_and_blocks_count_gaps(bool truncated)
    {
        using var provider=new DatabentoPositionClosingQuoteSource(new HttpClient(new BookHandler(truncated)),new(),()=>"test-key");
        if(truncated)await Assert.ThrowsAsync<InvalidDataException>(()=>provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3",true,"RAW C8450",1),Date,Boundary,default));
        else
        {
            var result=await provider.AcquireAsync(new("EXACT",42,"GLBX.MDP3",true,"RAW C8450",1),Date,Boundary,default);
            Assert.NotNull(result);Assert.Equal(Boundary.AddMinutes(-30),result.Quote.ObservedAtUtc);
            Assert.Equal(Boundary,result.Quote.Book!.VerifiedThroughUtc);Assert.Equal(.05m,result.Quote.MarkPrice);
            TomasAI.IFM.Domain.Trade.Shared.ClosingBookArchive.Verify(result.Quote,result.OriginalRecord,true);
        }
    }
    sealed class BookHandler(bool truncated):HttpMessageHandler
    {
        const string BookHeader="ts_recv,ts_event,rtype,publisher_id,instrument_id,action,sequence,flags,bid_px_00,ask_px_00,bid_sz_00,ask_sz_00";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent("{\"schema\":{\"mbp-1\":{\"end\":\"2027-01-01T00:00:00Z\"}}}")};
            var form=await request.Content!.ReadAsStringAsync(token);
            Assert.Contains("stype_in=raw_symbol",form,StringComparison.Ordinal);Assert.Contains("symbols=RAW",form,StringComparison.Ordinal);
            var method=request.RequestUri!.AbsolutePath;
            if(method.EndsWith("metadata.get_cost",StringComparison.Ordinal))return new(HttpStatusCode.OK){Content=new StringContent("0")};
            if(method.EndsWith("metadata.get_record_count",StringComparison.Ordinal))
            {
                Assert.Contains(Uri.EscapeDataString(Boundary.AddMinutes(10).ToString("O")),form,StringComparison.Ordinal);
                return new(HttpStatusCode.OK){Content=new StringContent(truncated?"2":"1")};
            }
            var extended=form.Contains(Uri.EscapeDataString(Boundary.AddMinutes(10).ToString("O")),StringComparison.Ordinal);
            var at=(Boundary.AddMinutes(-30).Ticks-DateTime.UnixEpoch.Ticks)*100;
            return new(HttpStatusCode.OK){Content=new StringContent(BookHeader+"\n"+(extended?$"{at},{at},1,1,42,M,123,128,9223372036854775807,50000000,0,18\n":""))};
        }
    }
    sealed class ErrorHandler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity){Content=new StringContent("{\"detail\":{\"case\":\"data_end_after_available_end\"}}")});
    }
    sealed class Handler(string cost,string csv,string? available=null):HttpMessageHandler
    {
        public int Calls {get;private set;}
        public List<string> Forms {get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent(
                "{\"end\":\"2027-01-01T00:00:00Z\",\"schema\":{\"mbp-1\":{\"end\":\""+(available??Boundary.AddSeconds(15).ToString("O"))+"\"}}}")};
            Forms.Add(await request.Content!.ReadAsStringAsync(token));
            if(request.RequestUri!.AbsolutePath.EndsWith("metadata.get_record_count",StringComparison.Ordinal))return new(HttpStatusCode.OK){Content=new StringContent("0")};
            return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(
                request.RequestUri!.AbsolutePath.EndsWith("metadata.get_cost",StringComparison.Ordinal)?cost:csv)};
        }
    }
}

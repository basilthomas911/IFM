using System.Net;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Domain.Trade.Shared;
using F=TomasAI.IFM.Tests.Support.ExpiryFixingTestScenario;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class ExpiryFixingSourceTests
{
    [Fact]
    public async Task One_official_fixing_is_shared_by_all_expiring_legs_and_zero_is_a_valid_call_value()
    {
        var handler=new Handler(F.Row());using var source=new DatabentoPositionClosingQuoteSource(new(handler),new(),()=>"test-key");
        var put=await source.AcquireAsync(F.Instrument(F.Convention()),F.Date,F.Boundary,default);
        var call=await source.AcquireAsync(F.Instrument(F.Convention(7450,true)),F.Date,F.Boundary,default);
        Assert.Equal(589.93m,put!.Quote.MarkPrice);Assert.Equal(0,call!.Quote.MarkPrice);Assert.Equal(1,handler.Downloads);
        VerifiedExpiryFixing.Verify(put.Quote,put.OriginalRecord);Assert.Equal(put.OriginalRecord,call.OriginalRecord);
    }
    [Theory]
    [InlineData("preliminary")]
    [InlineData("deleted")]
    [InlineData("wrong-date")]
    public async Task Missing_or_invalidated_fixing_stays_pending_and_does_not_poison_later_recovery(string shape)
    {
        var body=shape switch{"preliminary"=>F.Row(type:3,flags:2),"deleted"=>F.Row()+"\n"+F.Row(action:2),_=>F.Row(dateOffset:-1)};
        var handler=new Handler(body);using var source=new DatabentoPositionClosingQuoteSource(new(handler),new(),()=>"test-key");
        Assert.Null(await source.AcquireAsync(F.Instrument(F.Convention()),F.Date,F.Boundary,default));
        handler.Rows=F.Row();Assert.NotNull(await source.AcquireAsync(F.Instrument(F.Convention()),F.Date,F.Boundary,default));
        Assert.Equal(2,handler.Downloads);
    }
    [Theory]
    [InlineData(2,10252)]
    [InlineData(1,999)]
    public async Task Wrong_publisher_or_underlying_blocks_recovery(int publisher,int instrument)
    {
        var handler=new Handler(F.Row(publisher:publisher,instrument:instrument));using var source=new DatabentoPositionClosingQuoteSource(new(handler),new(),()=>"test-key");
        await Assert.ThrowsAsync<InvalidDataException>(()=>source.AcquireAsync(F.Instrument(F.Convention()),F.Date,F.Boundary,default));
    }
    sealed class Handler(string rows):HttpMessageHandler
    {
        public string Rows {get;set;}=rows;public int Downloads {get;private set;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent("{\"schema\":{\"statistics\":{\"end\":\"2026-11-20T22:00:00Z\"}}}")};
            var form=await request.Content!.ReadAsStringAsync(token);Assert.Contains("schema=statistics",form,StringComparison.Ordinal);Assert.Contains("symbols=10252",form,StringComparison.Ordinal);
            if(request.RequestUri!.AbsolutePath.EndsWith("metadata.get_cost",StringComparison.Ordinal))return new(HttpStatusCode.OK){Content=new StringContent("0")};
            Downloads++;return new(HttpStatusCode.OK){Content=new StringContent(F.Header+"\n"+Rows+"\n")};
        }
    }
}

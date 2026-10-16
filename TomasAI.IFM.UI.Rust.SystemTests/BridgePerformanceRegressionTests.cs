using MessagePack;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class BridgePerformanceRegressionTests
{
    [Fact] public async Task Unchanged_publication_reuses_bytes_and_refresh_invalidates_it() {
        await using var gateway=new ContractEditorGateway(_=>Task.FromResult(new ContractEditorResult("option-contract",[new("contract",new(){["kind"]="option"})],[])));
        var initial=gateway.WireSnapshot();
        Assert.Same(initial,gateway.WireSnapshot());
        Assert.Equal(0,gateway.Submit(new(1,Guid.NewGuid(),"option-contract","load",1,null,[])));
        await gateway.DrainAsync();
        var current=gateway.WireSnapshot();
        Assert.NotSame(initial,current);Assert.Same(current,gateway.WireSnapshot());
        Assert.Single(BridgeWire.Deserialize<ContractEditorPublication>(current).Result.Rows);
        Assert.Empty(BridgeWire.Deserialize<ContractEditorPublication>(initial).Result.Rows);
    }
    [Fact] public void Direct_document_encoding_preserves_portable_types_and_names() {
        var value=new{Amount=123.450m,Date=new DateOnly(2026,10,15),Id=Guid.Empty,State=DayOfWeek.Friday,
            Rows=new[]{new{Delta=.16,Text="ES / ? / ??"}},Missing=(string?)null};
        var bytes=BridgeDocument.Encode(value,true,true);
        var expected=BridgeDocument.Project(value,true,true);
        var encoded=BridgeDocument.Encode(expected);
        Assert.Equal(MessagePackSerializer.ConvertToJson(encoded),MessagePackSerializer.ConvertToJson(bytes));
    }
    [Fact] public async Task Owner_is_serial_bounded_and_drains_before_disposal() {
        await using var owner=new AsyncSessionOwner();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first=owner.Enqueue(async()=>{entered.SetResult();await release.Task;});
        await entered.Task;
        var second=owner.Enqueue(()=>Task.CompletedTask);
        Assert.Throws<InvalidOperationException>(()=>{_=owner.Enqueue(()=>Task.CompletedTask);});
        release.SetResult();await Task.WhenAll(first,second);
    }
    [Fact] public void Generated_market_contract_preserves_the_existing_display_schema() {
        var now=DateTimeOffset.Parse("2026-10-15T20:00:00Z");
        var contract=new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionContractReadModel(
            "ES-C",5000m,true,2m,2.5m,1,2,2.25m,1,.2,2.3,.16,.01,.2,-.1,.01,123,456,true,false,now,now,now,
            new DateOnly(2026,10,15),new DateOnly(2026,10,14));
        var chain=new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel("ES",new(2026,11,20),4900m,4800m,5200m,"Live",now,[contract]);
        AssertEquivalent(BridgeDocument.Encode(chain,true,true),BridgeWire.Serialize(OptionChainDisplay.From(chain)));
        var leg=new TomasAI.IFM.UI.Net.ViewModels.Trade.BrokerTradeLegData("ES-C","-SC",5000m,2m,2.5m,.16,true,QuoteAtUtc:now);
        AssertEquivalent(BridgeDocument.Encode(leg,true,true),BridgeWire.Serialize(SelectedLegDisplay.From(leg)));
        static void AssertEquivalent(byte[] a,byte[] b)=>Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(MessagePackSerializer.ConvertToJson(a)),
            System.Text.Json.Nodes.JsonNode.Parse(MessagePackSerializer.ConvertToJson(b))));
    }
    [Fact] public async Task Display_bursts_are_coalesced_without_losing_latest_state() {
        var count=0;var latest=0;var observed=0;
        var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var publisher=new CoalescedDisplayPublisher(()=>{Interlocked.Increment(ref count);observed=Volatile.Read(ref latest);completed.TrySetResult();},e=>completed.TrySetException(e));
        for(var i=1;i<=1000;i++){Volatile.Write(ref latest,i);publisher.Request();}
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1000,observed);Assert.InRange(count,1,2);
    }
    [Fact] public void Direct_envelope_formatter_matches_generated_schema_including_null_and_binary_fields() {
        var value=new ContractEditorPublication(1,42,"Ready",true,new("option-contract",
            [new("empty",[]),new("binary",new(){["kind"]="option"}){Data=[0,1,128,255]}],
            new(){["expiry"]=["2026-11-20"]},true,null){Documents=new(){["proof"]=[0,255]}},
            [new(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),"Completed","Ready")]);
        var buffer=new System.Buffers.ArrayBufferWriter<byte>();var writer=new MessagePackWriter(buffer);
        BridgeGeneratedResolver.Instance.GetFormatter<ContractEditorPublication>()!.Serialize(ref writer,value,BridgeWire.Options);writer.Flush();
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(MessagePackSerializer.ConvertToJson(buffer.WrittenMemory)),
            System.Text.Json.Nodes.JsonNode.Parse(MessagePackSerializer.ConvertToJson(BridgeWire.Serialize(value)))));
        Assert.Null(BridgeWire.Deserialize<ContractEditorPublication?>(BridgeWire.Serialize<ContractEditorPublication?>(null)));
    }
    [Fact] public void Large_document_does_not_corrupt_previously_returned_owned_bytes() {
        var first=BridgeDocument.Encode(new{Value=new string('x',100000)});
        var copy=first.ToArray();
        for(var i=0;i<10;i++)_=BridgeDocument.Encode(new{Value=new string('y',100000)});
        Assert.Equal(copy,first);
    }
}

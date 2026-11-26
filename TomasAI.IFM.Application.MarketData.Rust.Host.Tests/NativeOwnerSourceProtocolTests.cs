using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

public sealed class NativeOwnerSourceProtocolTests
{
    [Theory]
    [InlineData(true,2)]
    [InlineData(false,0)]
    public void Authoritative_status_exposes_configuration_without_changing_health_or_original_identities(bool enabled,long protocol)
    {
        var counter=new Dictionary<string,long>{{"Trade events",123}};
        var dataset=new MarketDataDatasetStatus("Futures","GLBX.MDP3",Guid.NewGuid(),Guid.NewGuid(),42,true,false,"Recovering","Original recovery",new(2026,11,26),Counters:counter);
        var option=dataset with{Role="Options",Counters=null};
        var original=new MarketDataBackendStatus(false,new(2026,11,26),[dataset,option],Guid.NewGuid(),17);
        var result=NativeOwnerRuntimeOperations.WithSourceProtocol(original,enabled);
        Assert.False(result.Ready);Assert.Equal(original.Revision,result.Revision);Assert.Equal(original.HostInstanceId,result.HostInstanceId);
        Assert.Equal(123,result.Datasets[0].Counters!["Trade events"]);
        foreach(var row in result.Datasets)Assert.Equal(protocol,row.Counters!["Configured source capture protocol"]);
        Assert.Equal(dataset,result.Datasets[0] with{Counters=counter});
        Assert.False(counter.ContainsKey("Configured source capture protocol"));
        Assert.Null(option.Counters);
    }
}

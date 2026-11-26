using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Storage;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Testing;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class RawFuturesSnapshotRetentionTests
{
    [Fact]
    public async Task Actual_inactive_EOF_publisher_keeps_authenticated_prefix_until_matching_resume()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-raw-checkpoint-"+Guid.NewGuid().ToString("N"));
        var batch=RawFuturesJournalFixture.Batch(4,true);var path=RawFuturesJournalFixture.Write(root,batch);
        var original=SHA256.HashData(File.ReadAllBytes(path));var owner=Path.ChangeExtension(path,".owner");
        var options=new RawFuturesStorageOptions{Enabled=true,Directory=root,Scope="retention"+Guid.NewGuid().ToString("N"),NatsUrl=Harness.ExplicitBrokerUrl.Value};
        await using var connections=new NatsConnectionManager();var transport=new RawFuturesStorageTransport(options,connections);
        using var publisher=new RawFuturesStoragePublisher(options,transport,NullLogger<RawFuturesStoragePublisher>.Instance);
        publisher.HoldSnapshotRetention(701);publisher.HoldSnapshotRetention(701);
        try
        {
            await publisher.StartAsync(default);await Until(()=>publisher.PublishedRecords==4&&publisher.Ready);
            Assert.False(RawFuturesJournal.OwnerIsActive(path));
            var committed=await RawFuturesJournal.ReadAsync(path,64,default);Assert.Null(committed.Batch);Assert.True(committed.Complete);
            await Task.Delay(100);Assert.True(publisher.Ready);Assert.Null(publisher.Failure);
            Assert.True(File.Exists(path+".cursor"));Assert.True(File.Exists(owner));Assert.Equal(original,SHA256.HashData(File.ReadAllBytes(path)));
            Assert.Throws<InvalidOperationException>(()=>publisher.ReleaseSnapshotRetention(702));
            Assert.Throws<InvalidOperationException>(()=>publisher.HoldSnapshotRetention(702));Assert.Equal(701UL,publisher.SnapshotRetentionLease);
            publisher.ReleaseSnapshotRetention(701);await Until(()=>!File.Exists(path));
            Assert.False(File.Exists(path+".cursor"));Assert.False(File.Exists(owner));Assert.Null(publisher.Failure);
        }
        finally
        {
            await publisher.StopAsync(default);
            if(Directory.Exists(root))Directory.Delete(root,true);
            var js=await transport.GetAsync(default);await js.DeleteStreamAsync(options.Stream);
        }
    }

    [Fact]
    public async Task Raw_prefix_retention_outlives_the_native_owner_and_publisher_shutdown()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-raw-checkpoint-stop-"+Guid.NewGuid().ToString("N"));
        var path=RawFuturesJournalFixture.Write(root,RawFuturesJournalFixture.Batch(2,false));var original=File.ReadAllBytes(path);
        var options=new RawFuturesStorageOptions{Enabled=true,Directory=root,Scope="retention"+Guid.NewGuid().ToString("N"),NatsUrl=Harness.ExplicitBrokerUrl.Value};
        await using var connections=new NatsConnectionManager();var transport=new RawFuturesStorageTransport(options,connections);
        using var publisher=new RawFuturesStoragePublisher(options,transport,NullLogger<RawFuturesStoragePublisher>.Instance);
        publisher.HoldSnapshotRetention(703);
        try
        {
            await publisher.StartAsync(default);await Until(()=>publisher.PublishedRecords==2&&publisher.Ready);
            await publisher.StopAsync(default);
            Assert.Equal(703UL,publisher.SnapshotRetentionLease);Assert.Equal(original,File.ReadAllBytes(path));
            Assert.True(File.Exists(path+".cursor"));Assert.True(File.Exists(Path.ChangeExtension(path,".owner")));
        }
        finally
        {
            await publisher.StopAsync(default);
            if(Directory.Exists(root))Directory.Delete(root,true);
            var js=await transport.GetAsync(default);await js.DeleteStreamAsync(options.Stream);
        }
    }
    static async Task Until(Func<bool> ready)
    {
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while(!ready())await Task.Delay(10,budget.Token);
    }
}

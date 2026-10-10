using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Cassandra;
using System.Text.Json;

namespace TomasAI.IFM.Application.Storage.Scylla.Benchmarks;

/// <summary>Prepared CQL snapshot and dated history workloads against disposable single-node Scylla containers.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0,launchCount:1,warmupCount:3,iterationCount:8)]
[MinIterationTime(500)]
public class ScyllaVersionBenchmarks
{
    public const string Keyspace="ifm_scylla_benchmark";
    public IEnumerable<string> Profiles => [Environment.GetEnvironmentVariable("IFM_SCYLLA_PROFILE") ?? throw new InvalidOperationException("Profile required.")];
    [ParamsSource(nameof(Profiles))] public string Profile {get;set;}="";
    ICluster cluster=null!;
    public ISession Session {get;private set;}=null!;
    PreparedStatement insert=null!,current=null!,latest=null!,history=null!,working=null!;
    readonly byte[] payload=new byte[1024];
    readonly Guid position=new("11111111-1111-1111-1111-111111111111");
    readonly LocalDate valueDate=new(2026,10,9);
    long sequence;int readSequence;
    public readonly HttpClient Admin=new(){BaseAddress=new Uri("http://127.0.0.1:59144/"),Timeout=TimeSpan.FromSeconds(90)};

    [GlobalSetup]
    public async Task Setup()
    {
        if(Environment.GetEnvironmentVariable("IFM_SCYLLA_BENCHMARK_PORT")!="59142") throw new InvalidOperationException("Dedicated benchmark endpoint required.");
        var version=Environment.GetEnvironmentVariable("IFM_SCYLLA_VERSION")!;
        var actual=JsonSerializer.Deserialize<string>(await Admin.GetStringAsync("storage_service/scylla_release_version"))!;
        if(!actual.StartsWith(version+"-",StringComparison.Ordinal) && actual!=version) throw new InvalidOperationException("Wrong server version: "+actual);
        var mode=JsonSerializer.Deserialize<string>(await Admin.GetStringAsync("v2/config/commitlog_sync"));
        if(mode!=Environment.GetEnvironmentVariable("IFM_SCYLLA_SYNC")) throw new InvalidOperationException("Wrong commitlog sync policy.");
        cluster=Cluster.Builder().AddContactPoint("127.0.0.1").WithPort(59142)
            .WithQueryTimeout(30000).WithSocketOptions(new SocketOptions().SetConnectTimeoutMillis(30000))
            .WithPoolingOptions(new PoolingOptions().SetCoreConnectionsPerHost(HostDistance.Local,2).SetMaxConnectionsPerHost(HostDistance.Local,32))
            .WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.LocalQuorum)).Build();
        Session=await cluster.ConnectAsync();
        await Session.ExecuteAsync(new SimpleStatement($"CREATE KEYSPACE IF NOT EXISTS {Keyspace} WITH replication={{'class':'NetworkTopologyStrategy','replication_factor':1}} AND durable_writes=true AND tablets={{'enabled':false}}"));
        Session.ChangeKeyspace(Keyspace);
        await Session.ExecuteAsync(new SimpleStatement("CREATE TABLE IF NOT EXISTS history (portfolioid int,fundid int,orderid int,tradeid int,positionid uuid,valuedate date,revision bigint,calculatedatutc timestamp,state text,requiresexit boolean,contenthash text,payload blob,PRIMARY KEY ((portfolioid,fundid,orderid,tradeid,positionid,valuedate),revision)) WITH CLUSTERING ORDER BY (revision DESC) AND compaction={'class':'SizeTieredCompactionStrategy'} AND compression={'sstable_compression':'LZ4Compressor','chunk_length_kb':'64'}"));
        await Session.ExecuteAsync(new SimpleStatement("CREATE TABLE IF NOT EXISTS current_snapshot (tradeid int PRIMARY KEY,revision bigint,payload blob) WITH compaction={'class':'SizeTieredCompactionStrategy'} AND compression={'sstable_compression':'LZ4Compressor','chunk_length_kb':'64'}"));
        await Session.ExecuteAsync(new SimpleStatement("CREATE TABLE IF NOT EXISTS seed_marker (id int PRIMARY KEY,completed boolean) WITH compaction={'class':'SizeTieredCompactionStrategy'} AND compression={'sstable_compression':'LZ4Compressor','chunk_length_kb':'64'}"));
        insert=await Session.PrepareAsync("INSERT INTO history (portfolioid,fundid,orderid,tradeid,positionid,valuedate,revision,calculatedatutc,state,requiresexit,contenthash,payload) VALUES (1,1,1,?,?,?,?,?,'Monitoring',false,'benchmark',?)");
        current=await Session.PrepareAsync("INSERT INTO current_snapshot (tradeid,revision,payload) VALUES (?,?,?)");
        latest=await Session.PrepareAsync("SELECT payload FROM current_snapshot WHERE tradeid=?");
        history=await Session.PrepareAsync("SELECT revision,payload FROM history WHERE portfolioid=1 AND fundid=1 AND orderid=1 AND tradeid=? AND positionid=? AND valuedate=? LIMIT 100");
        working=await Session.PrepareAsync("SELECT payload FROM history WHERE portfolioid=1 AND fundid=1 AND orderid=1 AND tradeid=? AND positionid=? AND valuedate=? AND revision<=? LIMIT 100");
        new Random(42).NextBytes(payload);
        using(var rows=await Session.ExecuteAsync(new SimpleStatement("SELECT completed FROM seed_marker WHERE id=1")))
        {
            if(!rows.Any())
            {
                // Bound seed concurrency; all seed work is outside measured iterations.
                await Parallel.ForEachAsync(Enumerable.Range(1,500),new ParallelOptions{MaxDegreeOfParallelism=mode=="batch" ? 4 : 32},async(trade,_)=>
                {
                    for(var revision=1;revision<=1000;revision+=32)
                    {
                        var seedBatch=new BatchStatement();seedBatch.SetBatchType(BatchType.Unlogged);
                        seedBatch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
                        for(var offset=0;offset<32 && revision+offset<=1000;offset++) seedBatch.Add(Bind(trade,revision+offset));
                        using(await Session.ExecuteAsync(seedBatch)){}
                    }
                    using(await Session.ExecuteAsync(current.Bind(trade,1000L,payload))){}
                });
                using(await Session.ExecuteAsync(new SimpleStatement("INSERT INTO seed_marker (id,completed) VALUES (1,true)"))){}
                await FlushAsync();
            }
        }
        sequence=0;readSequence=0;
        if(await LatestSnapshot()!=1024 || await History100()!=100 || await WorkingSetRead()!=100) throw new InvalidOperationException("Seed readback failed.");
        await WriteSnapshot();await AppendPlan();await SamePartitionBatch32();await ConcurrentWrite8();
        var output=Environment.GetEnvironmentVariable("IFM_SCYLLA_EVIDENCE")!;
        await File.WriteAllTextAsync(Path.Combine(output,Profile+"-validated.json"),JsonSerializer.Serialize(new{Profile,ServerVersion=actual,CommitlogSync=mode,Consistency="LOCAL_QUORUM",ReplicationFactor=1,Tablets=false,SeedRows=500000,PayloadBytes=1024}));
    }
    public BoundStatement Bind(int trade,long revision)=>insert.Bind(trade,position,valueDate,revision,DateTimeOffset.UnixEpoch,payload);
    public Task FlushAsync()=>FlushCoreAsync();
    async Task FlushCoreAsync(){using var response=await Admin.PostAsync("storage_service/keyspace_flush/"+Keyspace,null);response.EnsureSuccessStatusCode();}
    long Next(int count)=>Interlocked.Add(ref sequence,count)-count;
    [Benchmark] public async Task WriteSnapshot(){using(await Session.ExecuteAsync(current.Bind(9999,Next(1),payload))){} }
    [Benchmark] public async Task AppendPlan(){using(await Session.ExecuteAsync(Bind(9999,Next(1)%65536))){} }
    public async Task BatchAsync(int count,int trade=9999)
    {
        var start=Next(count);var batch=new BatchStatement();batch.SetBatchType(BatchType.Unlogged);batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        for(var i=0;i<count;i++) batch.Add(Bind(trade,(start+i)%65536));
        using(await Session.ExecuteAsync(batch)){}
    }
    [Benchmark] public Task SamePartitionBatch8()=>BatchAsync(8);
    [Benchmark] public Task SamePartitionBatch32()=>BatchAsync(32);
    [Benchmark] public Task SamePartitionBatch128()=>BatchAsync(128);
    [Benchmark] public async Task<int> LatestSnapshot(){using var rows=await Session.ExecuteAsync(latest.Bind(50));return rows.Single().GetValue<byte[]>("payload").Length;}
    [Benchmark] public async Task<int> History100(){using var rows=await Session.ExecuteAsync(history.Bind(50,position,valueDate));return rows.Count();}
    [Benchmark] public async Task<int> WorkingSetRead(){var n=readSequence++;using var rows=await Session.ExecuteAsync(working.Bind(n*137%500+1,position,valueDate,(long)(1000-(n/500)*97%901)));return rows.Count();}
    [Benchmark] public Task ConcurrentWrite8()=>Task.WhenAll(Enumerable.Range(0,8).Select(async i=>{using(await Session.ExecuteAsync(Bind(10000+i,Next(1)%65536))){} }));
    [GlobalCleanup] public Task Cleanup(){Session?.Dispose();cluster?.Dispose();Admin.Dispose();return Task.CompletedTask;}
}



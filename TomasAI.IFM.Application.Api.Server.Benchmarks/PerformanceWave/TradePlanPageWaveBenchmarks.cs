using System.Diagnostics;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using PlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave;
[MemoryDiagnoser]
public class TradePlanPageWaveBenchmarks
{
    PerformanceWaveTradePlanClient client = null!;
    int invocation;
    [Params(100,50,40)] public int PageSize {get;set;}
    [GlobalSetup] public async Task Setup()
    {
        client=new PerformanceWaveTradePlanClient();
        for(int i=0;i<4;i++) await client.ReadWorkload(i,PageSize,200);
    }
    [GlobalCleanup] public async Task Cleanup() => await client.DisposeAsync();
    [Benchmark] public Task<long> FirstPage() => client.ReadWorkload(invocation++,PageSize,0);
    [Benchmark] public Task<long> Read200Rows() => client.ReadWorkload(invocation++,PageSize,200);
}

sealed class PerformanceWaveTradePlanClient : IAsyncDisposable
{
    static readonly PlanId[] Plans = [
        Plan(701,1901,1501,new(2026,10,19)),
        Plan(701,1901,1501,new(2026,10,20)),
        Plan(701,1901,1501,new(2026,11,4)),
        Plan(703,4201,4601,new(2026,11,12))
    ];
    readonly NatsConnection nats = new(new NatsOpts { Url="nats://127.0.0.1:4222",RequestTimeout=TimeSpan.FromSeconds(12) });
    static readonly MessagePackSerializerOptions Options=TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options;

    public async Task<long> ReadWorkload(int invocation,int size,int rows)
    {
        var plan=Plans[invocation%Plans.Length];
        var (first,bytes)=await Read(plan,size,0,null,null);
        if (first.Items.Length!=size || first.MaximumRevision is null) throw new InvalidOperationException("Incomplete first page.");
        long totalBytes=bytes;
        for(var page=first; rows!=0 && page.Offset+page.Items.Length<rows;)
        {
            int next=page.Offset+page.Items.Length;
            (page,bytes)=await Read(plan,size,next,first.MaximumRevision,page.PagingState ?? throw new InvalidOperationException("Missing cursor."));
            if(page.Offset!=next || page.Items.Length!=size || page.MaximumRevision!=first.MaximumRevision)
                throw new InvalidOperationException("Inconsistent continuation.");
            totalBytes+=bytes;
        }
        return totalBytes;
    }

    public async Task Verify()
    {
        var reports=new List<object>();
        foreach(var plan in Plans)
        {
            var (anchor,_)=await Read(plan,100,0,null,null);
            if(anchor.MaximumRevision is null || anchor.TotalCount<201) throw new InvalidOperationException("Fixture is too short.");
            byte[][]? expected=null;
            var sizes=new List<object>();
            foreach(var size in new[]{100,50,40})
            {
                var rows=new List<byte[]>();
                var revisions=new List<long>();
                var lengths=new List<int>();
                byte[]? cursor=null;
                while(rows.Count<200)
                {
                    int offset=rows.Count;
                    var (page,len)=await Read(plan,size,offset,anchor.MaximumRevision,cursor);
                    if(page.Offset!=offset || page.MaximumRevision!=anchor.MaximumRevision || page.TotalCount!=anchor.TotalCount
                        || page.ChartEndUtc!=anchor.ChartEndUtc || page.Items.Length!=size || page.PagingState is null)
                        throw new InvalidOperationException("Page metadata mismatch.");
                    if(offset==0 && !SameBytes(page.ChartPoints,anchor.ChartPoints)) throw new InvalidOperationException("Chart points changed.");
                    if(offset==size)
                    {
                        var (jump,_)=await Read(plan,size,offset,anchor.MaximumRevision,null);
                        if(!SameBytes(page.Items,jump.Items)) throw new InvalidOperationException("Cursor and offset disagree.");
                    }
                    foreach(var row in page.Items)
                    {
                        rows.Add(MessagePackSerializer.Serialize(row,Options));
                        revisions.Add(row.PlanRevision);
                    }
                    lengths.Add(len);
                    cursor=page.PagingState;
                }
                if (revisions[0]>anchor.MaximumRevision || revisions.Zip(revisions.Skip(1)).Any(p=>p.First<=p.Second))
                    throw new InvalidOperationException("Revision ordering changed.");
                if(expected is not null && !rows.Zip(expected).All(p=>p.First.AsSpan().SequenceEqual(p.Second)))
                    throw new InvalidOperationException("Rows changed across page sizes.");
                expected ??= rows.ToArray();
                sizes.Add(new {size, firstBytes=lengths[0], maxBytes=lengths.Max(), bytes200=lengths.Sum(),requests=lengths.Count,
                    firstRevision=revisions[0],lastRevision=revisions[^1]});
            }
            var (mixed,_)=await Read(plan,40,100,anchor.MaximumRevision,
                anchor.PagingState ?? throw new InvalidOperationException("Missing 100-row cursor."));
            if(mixed.Offset!=100 || mixed.MaximumRevision!=anchor.MaximumRevision || mixed.Items.Length!=40
                || !mixed.Items.Select(row=>MessagePackSerializer.Serialize(row,Options)).Zip(expected!.Skip(100))
                    .All(p=>p.First.AsSpan().SequenceEqual(p.Second)))
                throw new InvalidOperationException("Existing 100-row cursor failed at size 40.");
            reports.Add(new { plan=plan.Format(), anchor.TotalCount, anchor.MaximumRevision, anchor.ChartEndUtc,
                cursorFrom100To40=true,
                chartPoints=anchor.ChartPoints.Length, verifiedOrderedRows=expected!.Length, pages=sizes });
        }
        Console.WriteLine(JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
    }

    static bool SameBytes<T>(T a,T b) =>
        MessagePackSerializer.Serialize(a,Options).AsSpan().SequenceEqual(MessagePackSerializer.Serialize(b,Options));
    async Task<(StrategyTradePlanHistoryPage Page,int Bytes)> Read(PlanId plan,int size,int offset,long? anchor,byte[]? token)
    {
        var key=plan.Format();
        var subject=new ActorSubject(ActorType.Query,GetIronCondorTradePlanHistoryQuery.Actor,GetIronCondorTradePlanHistoryQuery.Verb,key);
        var query=new GetIronCondorTradePlanHistoryQuery(subject,new ActorEntityId(key),plan,size,token,offset,anchor);
        var response=await nats.RequestAsync<byte[],byte[]>(subject.ToString(),MessagePackSerializer.Serialize(query,Options));
        var payload=response.Data ?? throw new InvalidOperationException("Empty NATS reply.");
        var result=MessagePackSerializer.Deserialize<ServiceResult<StrategyTradePlanHistoryPage>>(payload,Options);
        if(!result.Success || result.Value is null) throw new InvalidOperationException(result.ErrorMessage);
        return (result.Value,payload.Length);
    }

    static PlanId Plan(int fund,int order,int trade,DateOnly date) =>
        new(StrategyPositionId.Create(new(101,fund,order,trade),TradeStrategyKind.IronCondor),date);
    public ValueTask DisposeAsync()=>nats.DisposeAsync();
}

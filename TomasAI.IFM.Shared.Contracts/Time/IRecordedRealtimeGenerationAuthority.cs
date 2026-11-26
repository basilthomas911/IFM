using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.Time;

/// <summary>Explicit original-generation permission from authenticated captured handler evidence; not a financial approval.</summary>
public interface IRecordedRealtimeGenerationAuthority
{
    /// <summary>Accepts only the exact original source and a live isolated receiving generation.</summary>
    bool Allows(RecordedRealtimeGenerationAcceptance original,Guid currentGenerationId);
}

/// <summary>Written only after normal generation, position revision and input identity checks pass, before calculation.</summary>
[MessagePackObject]
public sealed record RecordedRealtimeGenerationAcceptance
{
    [Key(0)] public int Version {get;init;}=1;
    [Key(1)] public ActorSubject Subject {get;init;}
    [Key(2)] public Guid EventId {get;init;}
    [Key(3)] public Guid OriginalGenerationId {get;init;}
    [Key(4)] public string PayloadSha256 {get;init;}="";
    [Key(5)] public long PositionSequence {get;init;}
    [Key(6)] public long RouteGeneration {get;init;}
}

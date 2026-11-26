namespace TomasAI.IFM.Shared.Time;

/// <summary>Declares an explicitly isolated replay's authoritative recorded realtime sources.</summary>
/// <remarks>Implemented only by the Test replay clock. It suppresses a matching source producer,
/// never its handlers, durable recovery or elapsed-time deadlines.</remarks>
public interface IRecordedRealtimeSourceReplay
{
    /// <summary>Whether this exact actor and verb must come from the original recorded source.</summary>
    bool UsesRecordedSource(string actor, string verb);
}

namespace TomasAI.IFM.Shared.Time;

/// <summary>Optional immutable provenance supplied by an explicitly configured application clock.</summary>
/// <remarks>Live System clocks use the system domain. Controlled clocks declare their own domain and lifetime;
/// this contract does not select a feed, start replay or change any process-global clock.</remarks>
public interface IMessageClockIdentity
{
    string ClockDomainId { get; }
    Guid? RunId { get; }
    Guid? Generation { get; }
}

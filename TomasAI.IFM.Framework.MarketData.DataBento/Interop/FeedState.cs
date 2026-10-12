using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is an existing meaningful contract value; introducing an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
public enum FeedState : uint
{
    Created = 1,
    Subscribed = 2,
    Starting = 3,
    ConsumerSetup = 4,
    Running = 5,
    Stopping = 6,
    Stopped = 7,
    Faulted = 8
}

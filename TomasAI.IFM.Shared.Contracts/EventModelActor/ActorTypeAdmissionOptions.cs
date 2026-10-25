using System.Numerics;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed class ActorTypeAdmissionOptions
{
    public long MessageLimit { get; set; }
    public long ByteLimit { get; set; }
}

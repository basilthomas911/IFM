using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
readonly record struct ActorAdmissionCharge(bool IsTracked, ActorType ActorType, int PayloadBytes)
{
    // Release may run on a different thread than reservation.
    public int StripeIndex { get; init; }
}

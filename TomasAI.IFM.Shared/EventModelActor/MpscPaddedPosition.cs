using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Shared.EventModelActor;

[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct MpscPaddedPosition
{
    [FieldOffset(64)] public long Value;
}

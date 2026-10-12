using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.Shared.EventModelActor;

[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct SpscPaddedPosition
{
    [FieldOffset(64)] public int Value;
}

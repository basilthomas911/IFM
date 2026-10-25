using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct MpscPaddedPosition
{
    [FieldOffset(64)] public long Value;
}

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;


namespace TomasAI.IFM.Shared.EventModelActor;

// Cache-line isolation to avoid false sharing.
// Each PaddedInt occupies 128 bytes with Value centered at offset 64,
// ensuring isolation on both 64-byte (x86) and 128-byte (ARM/Apple M) cache lines.
// Defined outside the generic class because generic types cannot have explicit layout.
[StructLayout(LayoutKind.Explicit, Size = 128)]
struct PaddedInt
{
    [FieldOffset(64)] public int Value;
}

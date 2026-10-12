using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new ProcessorCandidate instance.</summary>
/// <param name="Location">The location.</param>
/// <param name="CpuSetId">The cpu set id.</param>
/// <param name="CoreIndex">The core index.</param>
/// <param name="NumaNodeIndex">The numa node index.</param>
/// <param name="EfficiencyClass">The efficiency class.</param>
/// <param name="CoreKind">The core kind.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProcessorCandidate(
    LogicalProcessorLocation Location,
    uint CpuSetId,
    int CoreIndex,
    ushort NumaNodeIndex,
    byte EfficiencyClass,
    ProcessorCoreKind CoreKind = ProcessorCoreKind.Unknown);

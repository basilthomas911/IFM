using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new ProcessorPairResolution instance.</summary>
/// <param name="NativeProducer">The native producer.</param>
/// <param name="ManagedDrain">The managed drain.</param>
/// <param name="PerformanceCoreClassificationAvailable">The performance core classification available.</param>
/// <param name="PerformanceCoresSelected">The performance cores selected.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProcessorPairResolution(
    LogicalProcessorLocation NativeProducer,
    LogicalProcessorLocation ManagedDrain,
    bool PerformanceCoreClassificationAvailable,
    bool PerformanceCoresSelected);

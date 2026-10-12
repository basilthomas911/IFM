namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new LogicalProcessorLocation instance.</summary>
/// <param name="ProcessorGroup">The processor group.</param>
/// <param name="LogicalProcessorIndex">The logical processor index.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct LogicalProcessorLocation(
    ushort ProcessorGroup,
    ushort LogicalProcessorIndex);

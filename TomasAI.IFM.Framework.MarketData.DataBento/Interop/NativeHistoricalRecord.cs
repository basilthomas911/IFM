using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 120)]
internal unsafe struct NativeHistoricalRecord
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeHistoricalRecordKind RecordKind;
    public NativeHistoricalSchema Schema;
    public uint InstrumentId;
    public ushort PublisherId;
    public ushort ConditionFlags;
    public long EventTimestampNanoseconds;
    public long SourceSequence;
    public long OpenPrice;
    public long HighPrice;
    public long LowPrice;
    public long CloseOrTradePrice;
    public ulong VolumeOrSize;
    public byte Action;
    public byte Side;
    private fixed byte _reserved[6];
    public fixed byte Symbol[32];
}

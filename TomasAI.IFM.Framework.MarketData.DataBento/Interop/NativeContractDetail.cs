using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 192)]
internal unsafe struct NativeContractDetail
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeContractDetailFlags Flags;
    public uint InstrumentId;
    public ushort PublisherId;
    public byte ContractKind;
    public byte MaturityMonth;
    public byte MaturityDay;
    public byte MaturityWeek;
    public ushort MaturityYear;
    public uint UnderlyingId;
    public int ContractMultiplier;
    public ulong RawInstrumentId;
    public long StrikePrice;
    public long MinimumPriceIncrement;
    public long MinimumPriceIncrementAmount;
    public ulong ExpirationTimestampNanoseconds;
    public ulong ActivationTimestampNanoseconds;
    public NativeUtf8Slice RawSymbol;
    public NativeUtf8Slice Asset;
    public NativeUtf8Slice Underlying;
    public NativeUtf8Slice Currency;
    public NativeUtf8Slice SettlementCurrency;
    public NativeUtf8Slice Exchange;
    public NativeUtf8Slice SecurityType;
    public NativeUtf8Slice Cfi;
    public NativeUtf8Slice UnitOfMeasure;
    public fixed ulong Reserved[5];
}

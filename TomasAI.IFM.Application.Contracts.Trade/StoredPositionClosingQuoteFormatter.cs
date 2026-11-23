using MessagePack;
using MessagePack.Formatters;
namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Retains the original thirteen-field wire representation and evidence IDs for legacy quotes.</summary>
public sealed class StoredPositionClosingQuoteFormatter : IMessagePackFormatter<StoredPositionClosingQuote?>
{
    public void Serialize(ref MessagePackWriter writer, StoredPositionClosingQuote? value, MessagePackSerializerOptions options)
    {
        if(value is null){writer.WriteNil();return;}
        writer.WriteArrayHeader(value.ExpiryFixing is not null?15:value.Book is null?13:14);
        writer.Write(value.EvidenceId);writer.Write(value.ContractId);
        options.Resolver.GetFormatterWithVerify<DateOnly>().Serialize(ref writer,value.ValueDate,options);
        writer.Write(value.InstrumentId);writer.Write(value.EventTimestampNanoseconds);writer.Write(value.ReceiveTimestampNanoseconds);
        writer.Write(value.SourceSequence);
        var decimals=options.Resolver.GetFormatterWithVerify<decimal>();
        decimals.Serialize(ref writer,value.Bid,options);decimals.Serialize(ref writer,value.Ask,options);
        writer.Write(value.BidSize);writer.Write(value.AskSize);writer.Write(value.SourceArchiveSha256);writer.Write(value.HasBid);
        if(value.Book is not null || value.ExpiryFixing is not null)options.Resolver.GetFormatterWithVerify<VerifiedClosingBook>().Serialize(ref writer,value.Book,options);
        if(value.ExpiryFixing is not null)options.Resolver.GetFormatterWithVerify<VerifiedExpiryFixing>().Serialize(ref writer,value.ExpiryFixing,options);
    }
    public StoredPositionClosingQuote? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.TryReadNil())return null!;
        options.Security.DepthStep(ref reader);
        try
        {
            var count=reader.ReadArrayHeader();var q=new StoredPositionClosingQuote();
            for(var i=0;i<count;i++) q=i switch
            {
                0=>q with{EvidenceId=reader.ReadString()!},1=>q with{ContractId=reader.ReadString()!},
                2=>q with{ValueDate=options.Resolver.GetFormatterWithVerify<DateOnly>().Deserialize(ref reader,options)},
                3=>q with{InstrumentId=reader.ReadUInt32()},4=>q with{EventTimestampNanoseconds=reader.ReadInt64()},
                5=>q with{ReceiveTimestampNanoseconds=reader.ReadInt64()},6=>q with{SourceSequence=reader.ReadUInt32()},
                7=>q with{Bid=options.Resolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader,options)},
                8=>q with{Ask=options.Resolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader,options)},
                9=>q with{BidSize=reader.ReadUInt32()},10=>q with{AskSize=reader.ReadUInt32()},
                11=>q with{SourceArchiveSha256=reader.ReadString()!},12=>q with{HasBid=reader.ReadBoolean()},
                13=>q with{Book=options.Resolver.GetFormatterWithVerify<VerifiedClosingBook>().Deserialize(ref reader,options)},
                14=>q with{ExpiryFixing=options.Resolver.GetFormatterWithVerify<VerifiedExpiryFixing>().Deserialize(ref reader,options)},
                _=>Skip(ref reader,q)
            };
            return q;
        }
        finally {reader.Depth--;}
    }
    static StoredPositionClosingQuote Skip(ref MessagePackReader reader,StoredPositionClosingQuote q){reader.Skip();return q;}
}

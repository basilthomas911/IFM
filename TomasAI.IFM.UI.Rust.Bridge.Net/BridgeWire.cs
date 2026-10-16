using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using TomasAI.IFM.UI.Net.Models.Reference;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Portable in-process wire format. Never uses global broker/storage compression options.</summary>
public static class BridgeWire
{
    public const uint AbiVersion = 4;
    public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create([new LookupWireFormatter(), new ContractPublicationFormatter()], [BridgeGeneratedResolver.Instance, ContractlessStandardResolverAllowPrivate.Instance]))
        .WithCompression(MessagePackCompression.None)
        .WithSecurity(MessagePackSecurity.UntrustedData);

    public static byte[] Serialize<T>(T value) => MessagePackSerializer.Serialize(value, Options);
    public static T Deserialize<T>(ReadOnlyMemory<byte> bytes) => MessagePackSerializer.Deserialize<T>(bytes, Options);
}

[MessagePackObject]
public sealed record BridgeIntentHeader([property: Key("view")] string View);

[MessagePackObject]
public sealed record BridgeStringCatalog(
    [property: Key("version")] int Version,
    [property: Key("locale")] string Locale,
    [property: Key("strings")] Dictionary<string,string> Strings);

/// <summary>Keeps the existing shared UI model unchanged and timestamps readable by Rust without .NET extensions.</summary>
sealed class LookupWireFormatter : IMessagePackFormatter<LookupTypeUiModel?>
{
    public void Serialize(ref MessagePackWriter writer, LookupTypeUiModel? value, MessagePackSerializerOptions options)
    {
        if(value is null){writer.WriteNil();return;}
        writer.WriteMapHeader(6);
        writer.Write("lookupTypeName");writer.Write(value.LookupTypeName);
        writer.Write("shortCode");writer.Write(value.ShortCode);
        writer.Write("orderId");writer.Write(value.OrderId);
        writer.Write("description");writer.Write(value.Description);
        writer.Write("createdOn");writer.Write(value.CreatedOn.ToString("O",CultureInfo.InvariantCulture));
        writer.Write("createdBy");writer.Write(value.CreatedBy);
    }
    public LookupTypeUiModel? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if(reader.TryReadNil())return null;
        options.Security.DepthStep(ref reader);
        try
        {
            string name="",code="",description="",by="";int order=0;DateTime created=default;
            var count=reader.ReadMapHeader();
            for(var i=0;i<count;i++)
            {
                switch(reader.ReadString())
                {
                    case "lookupTypeName":name=reader.ReadString()??"";break;
                    case "shortCode":code=reader.ReadString()??"";break;
                    case "orderId":order=reader.ReadInt32();break;
                    case "description":description=reader.ReadString()??"";break;
                    case "createdOn":created=DateTime.Parse(reader.ReadString()!,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);break;
                    case "createdBy":by=reader.ReadString()??"";break;
                    default:reader.Skip();break;
                }
            }
            return new(name,code,order,description,created,by);
        }
        finally{reader.Depth--;}
    }
}

[GeneratedMessagePackResolver]
internal partial class BridgeGeneratedResolver;

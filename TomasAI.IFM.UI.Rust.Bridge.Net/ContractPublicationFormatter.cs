using MessagePack;
using MessagePack.Formatters;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Writes the high-volume envelope directly; preserves the ABI's named maps and binary row documents.</summary>
[ExcludeFormatterFromSourceGeneratedResolver]
sealed class ContractPublicationFormatter : IMessagePackFormatter<ContractEditorPublication?>
{
    static ReadOnlySpan<byte> RowIdPrefix => [0x83,0xa2,0x69,0x64];
    static ReadOnlySpan<byte> FieldsPrefix => [0xa6,0x66,0x69,0x65,0x6c,0x64,0x73];
    static ReadOnlySpan<byte> DataPrefix => [0xa4,0x64,0x61,0x74,0x61];
    public void Serialize(ref MessagePackWriter w,ContractEditorPublication? v,MessagePackSerializerOptions options){
        if(v is null){w.WriteNil();return;}
        w.WriteMapHeader(6);w.Write("schema");w.Write(v.Schema);w.Write("generation");w.Write(v.Generation);
        w.Write("status");w.Write(v.Status);w.Write("busy");w.Write(v.Busy);w.Write("result");
        var result=v.Result;w.WriteMapHeader(6);w.Write("view");w.Write(result.View);w.Write("rows");w.WriteArrayHeader(result.Rows.Length);
        foreach(var row in result.Rows){
            w.WriteRaw(RowIdPrefix);w.Write(row.Id);
            w.WriteRaw(FieldsPrefix);Strings(ref w,row.Fields);
            w.WriteRaw(DataPrefix);w.Write(row.Data);
        }
        w.Write("choices");w.WriteMapHeader(result.Choices.Count);
        foreach(var pair in result.Choices){w.Write(pair.Key);w.WriteArrayHeader(pair.Value.Length);foreach(var item in pair.Value)w.Write(item);}
        w.Write("hasMore");w.Write(result.HasMore);w.Write("context");Strings(ref w,result.Context);
        w.Write("documents");w.WriteMapHeader(result.Documents.Count);foreach(var pair in result.Documents){w.Write(pair.Key);w.Write(pair.Value);}
        // ContractEditorResult has six map members (including Documents).
        w.Write("outcomes");w.WriteArrayHeader(v.Outcomes.Length);
        var guid=options.Resolver.GetFormatterWithVerify<Guid>();
        foreach(var outcome in v.Outcomes){w.WriteMapHeader(3);w.Write("requestId");guid.Serialize(ref w,outcome.RequestId,options);w.Write("state");w.Write(outcome.State);w.Write("message");w.Write(outcome.Message);}
    }
    static void Strings(ref MessagePackWriter writer,Dictionary<string,string>? values){
        if(values is null){writer.WriteNil();return;}writer.WriteMapHeader(values.Count);foreach(var pair in values){writer.Write(pair.Key);writer.Write(pair.Value);}
    }
    public ContractEditorPublication? Deserialize(ref MessagePackReader reader,MessagePackSerializerOptions options)
        =>BridgeGeneratedResolver.Instance.GetFormatter<ContractEditorPublication>()!.Deserialize(ref reader,options);
}

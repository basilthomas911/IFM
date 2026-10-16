using System.Runtime.CompilerServices;
using MessagePack;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public class BridgeWireTests
{
    [Fact]
    public void Wire_options_are_uncompressed_and_isolated_from_global_defaults()
    {
        Assert.Equal(MessagePackCompression.None,BridgeWire.Options.Compression);
        var options=MessagePackSerializer.DefaultOptions;
        try
        {
            MessagePackSerializer.DefaultOptions=options.WithCompression(MessagePackCompression.Lz4BlockArray);
            var row=new StatusDisplayRow("id","12:34:56","ES / \u03b4 / \u65e5\u672c");
            var bytes=BridgeWire.Serialize(row);
            Assert.Equal(MessagePackType.Map,new MessagePackReader(bytes).NextMessagePackType);
            Assert.Equal(row,BridgeWire.Deserialize<StatusDisplayRow>(bytes));
        }
        finally{MessagePackSerializer.DefaultOptions=options;}
    }

    [Fact]
    public void Existing_lookup_model_round_trips_without_changing_shared_models()
    {
        var lookup=new LookupTypeUiModel("Symbol","ES",int.MaxValue,"S&P futures",new(2026,10,15,12,34,56,DateTimeKind.Utc),"developer");
        var intent=new EditorIntent(1,Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),"lookup","update",long.MaxValue,lookup);
        var copy=BridgeWire.Deserialize<EditorIntent>(BridgeWire.Serialize(intent));
        Assert.Equal(intent,copy);
        Assert.Equal(DateTimeKind.Utc,copy.Lookup!.CreatedOn.Kind);
        Assert.Null(BridgeWire.Deserialize<EditorIntent>(BridgeWire.Serialize(intent with{Lookup=null})).Lookup);
        var publication=new EditorPublication(1,42,"Ready",false,[lookup],[new(intent.RequestId,"Completed","Saved")]);
        Assert.Equal(lookup,Assert.Single(BridgeWire.Deserialize<EditorPublication>(BridgeWire.Serialize(publication)).Rows));
    }

    [Fact]
    public unsafe void Abi_rejects_json_truncated_and_compressed_requests()
    {
        delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;
        ApiV4 api;Assert.Equal(0,get(4,(ApiV1*)&api,(uint)sizeof(ApiV4)));
        ulong handle;Assert.Equal(0,api.Base.Create(&handle));
        try
        {
            var intent=new ContractEditorIntent(1,Guid.NewGuid(),"parameter-sets","load",1,null,[]);
            var bytes=BridgeWire.Serialize(intent);
            var legacy=System.Text.Encoding.UTF8.GetBytes("{\"schema\":1,\"view\":\"parameter-sets\"}");
            fixed(byte* p=legacy)Assert.Equal(-1,api.Submit(handle,p,legacy.Length));
            fixed(byte* p=bytes)Assert.Equal(-1,api.Submit(handle,p,bytes.Length-1));
            var compressed=MessagePackSerializer.Serialize(intent,BridgeWire.Options.WithCompression(MessagePackCompression.Lz4BlockArray).WithCompressionMinLength(1));
            fixed(byte* p=compressed)Assert.Equal(-1,api.Submit(handle,p,compressed.Length));
            fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));
        }
        finally{Assert.Equal(0,api.Base.Destroy(handle));}
    }
    [Fact] public unsafe void Abi5_capacity_retry_does_not_advance_cursor_and_unchanged_is_empty() {
        delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;
        ApiV5 api;Assert.Equal(-1,get(5,(ApiV1*)&api,(uint)sizeof(ApiV4)));
        Assert.Equal(0,get(5,(ApiV1*)&api,(uint)sizeof(ApiV5)));
        ulong handle,cursor=0;Assert.Equal(0,api.Base.Base.Create(&handle));
        try {
            int required;long produced,source;
            Assert.Equal(1,api.CopyChanged(handle,8,&cursor,null,0,&required,&produced,&source));
            Assert.Equal(0ul,cursor);Assert.True(required>0);
            var bytes=new byte[required];
            fixed(byte* data=bytes)Assert.Equal(0,api.CopyChanged(handle,8,&cursor,data,bytes.Length,&required,&produced,&source));
            Assert.True(cursor>0);Assert.Equal(1,BridgeWire.Deserialize<ContractEditorPublication>(bytes).Schema);
            Assert.Equal(2,api.CopyChanged(handle,8,&cursor,null,0,&required,&produced,&source));
            Assert.Equal(0,required);
        }finally{Assert.Equal(0,api.Base.Base.Destroy(handle));}
    }
    [Fact] public void Virtual_grid_array_uses_the_same_keys_as_the_generated_row_formatter() {
        var row=VirtualGridDataset.Create(1)[0];
        var array=BridgeWire.Serialize(new[]{row});
        Assert.Equal("[{"+MessagePackSerializer.ConvertToJson(BridgeWire.Serialize(row))[1..]+"]",MessagePackSerializer.ConvertToJson(array));
    }
    [Fact] public void Nested_wire_alias_does_not_change_plain_display_document_names() {
        var nested=new { item=new AliasedDisplay("value") };
        Assert.Equal("{\"item\":{\"wire_name\":\"value\"}}",MessagePackSerializer.ConvertToJson(BridgeWire.Serialize(nested)));
        Assert.Equal("{\"PascalName\":\"value\"}",MessagePackSerializer.ConvertToJson(BridgeDocument.Encode(nested.item)));
    }
}

[MessagePackObject]
public sealed record AliasedDisplay([property:Key("wire_name")] string PascalName);

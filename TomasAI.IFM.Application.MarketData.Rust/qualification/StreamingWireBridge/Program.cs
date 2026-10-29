using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

// The existing C# envelope/formatter is the immutable wire oracle. This tool
// exports source-derived bytes; no Rust serializer participates in expected data.
var worker=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
var generation=Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100");
var rows=new List<object>();
foreach(var (lane,role,dataset) in new[]{(1,"Futures","GLBX.MDP3"),(2,"Futures","XCBF.PITCH"),(3,"Options","GLBX.MDP3")})
foreach(var kind in Enum.GetValues<DatasetPublicationKind>())
{
    var value=new DatasetPublicationEnvelope{Dataset=dataset,ValueDate=new(2026,10,27),WorkerInstanceId=worker,GenerationId=generation,
        ManifestRevision=127,PublicationSequence=65536,Kind=kind,Payload=[0x92,0xc0,0xc3]};
    var bytes=MessagePackSerializer.Serialize(value,TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options);
    var actual=MessagePackSerializer.Deserialize<DatasetPublicationEnvelope>(bytes,TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options);
    if(actual.WorkerInstanceId!=worker||actual.ValueDate!=value.ValueDate)throw new InvalidDataException("C# wire oracle round trip failed.");
    rows.Add(new{lane,valueDate=value.ValueDate.DayNumber,worker=worker.ToByteArray(),generation=generation.ToByteArray(),
        revision=value.ManifestRevision,sequence=value.PublicationSequence,kind=(byte)kind,payload=value.Payload,
        subject=$"ifm.development.marketdata.v1.publications.{role}.{worker:N}",envelope=bytes,
        content=MessagePackSerializer.Serialize(value,TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.ContentOptions)});
}
await File.WriteAllTextAsync(args[0],System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS exported {rows.Count} C# envelope/subject oracles; actual existing MessagePack formatter and all role/kind combinations.");

if(args.Length==2) {
    var native=System.Text.Json.JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[1]))!;
    if(native.Length!=rows.Count)throw new InvalidDataException("Native fixture count mismatch.");
    var oracles=System.Text.Json.JsonSerializer.SerializeToElement(rows);
    for(var i=0;i<native.Length;i++) {
        var decoded=MessagePackSerializer.Deserialize<DatasetPublicationEnvelope>(Convert.FromBase64String(native[i]),TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options);
        var content=MessagePackSerializer.Serialize(decoded,TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.ContentOptions);
        if(!content.SequenceEqual(oracles[i].GetProperty("content").GetBytesFromBase64()))throw new InvalidDataException("Native envelope changed the C# decoded contract: "+i);
    }
    Console.WriteLine($"PASS C# production formatter decoded {native.Length} Rust LZ4 envelopes with exact original contract bytes.");
}

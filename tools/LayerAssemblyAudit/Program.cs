using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
var output = new List<object>();
if(args.Length>2)
{
 var root=Path.GetFullPath(args[2]);var findings=new List<object>();var audited=0;
 var compositionRoots=new HashSet<string>{"TomasAI.IFM.Application.Api.Server.Core","TomasAI.IFM.Application.Api.Server","TomasAI.IFM.Application.MarketData.Host","TomasAI.IFM.Application.MarketData.Host.Worker","TomasAI.IFM.Application.ServerManager","TomasAI.IFM.Application.DatabaseBackup.Console"};
 foreach(var directory in Directory.EnumerateDirectories(root,"TomasAI.IFM.*"))
 {
  var name=Path.GetFileName(directory);
  if(new[]{"Tests","Benchmarks","Probe","QualificationHost","TestProcess"}.Any(name.Contains))continue;
  var project=Directory.EnumerateFiles(directory,"*.csproj").FirstOrDefault();if(project is null)continue;
  var projectName=Path.GetFileNameWithoutExtension(project);
  var xml=System.Xml.Linq.XDocument.Load(project);
  var assemblyName=xml.Descendants("AssemblyName").FirstOrDefault()?.Value??projectName;
  var path=Path.Combine(args[0],projectName,"debug",assemblyName+".dll");
  if(!File.Exists(path)){findings.Add(new{consumer=name,owner="<not built>",reason="Production assembly unavailable"});continue;}
  audited++;
  using var input=File.OpenRead(path);using var image=new PEReader(input);var reader=image.GetMetadataReader();
  foreach(var referenceHandle in reader.AssemblyReferences)
  {
   var owner=reader.GetString(reader.GetAssemblyReference(referenceHandle).Name);string? reason=null;
   if(name.StartsWith("TomasAI.IFM.Domain.",StringComparison.Ordinal))
   {
    if(owner.StartsWith("TomasAI.IFM.Framework.",StringComparison.Ordinal))reason="Domain uses Framework";
    else if(owner.StartsWith("TomasAI.IFM.Domain.",StringComparison.Ordinal) && owner!=assemblyName)reason="Domain uses another Domain implementation";
    else if(new[]{"NATS.","Npgsql","Cassandra","Dapper","AWSSDK.","Hazelcast","StackExchange.Redis"}.Any(p=>owner.StartsWith(p,StringComparison.Ordinal)))reason="Domain uses provider SDK";
   }
   else if(name.StartsWith("TomasAI.IFM.Framework.",StringComparison.Ordinal) && (owner.StartsWith("TomasAI.IFM.Application.",StringComparison.Ordinal) || owner.StartsWith("TomasAI.IFM.Domain.",StringComparison.Ordinal)))reason="Framework uses higher layer";
   else if(name.StartsWith("TomasAI.IFM.Application.",StringComparison.Ordinal) && !compositionRoots.Contains(name) && owner.StartsWith("TomasAI.IFM.Domain.",StringComparison.Ordinal))reason="Reusable Application uses Domain";
   else if(name.StartsWith("TomasAI.IFM.Shared",StringComparison.Ordinal) && owner.StartsWith("TomasAI.IFM.",StringComparison.Ordinal) && owner!="TomasAI.IFM.Shared")reason="Shared uses implementation assembly";
   if(reason is not null)findings.Add(new{consumer=name,owner,reason,types=reader.TypeReferences.Select(h=>reader.GetTypeReference(h)).Where(t=>t.ResolutionScope.Kind==HandleKind.AssemblyReference && reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)t.ResolutionScope).Name)==owner).Select(t=>reader.GetString(t.Namespace)+"."+reader.GetString(t.Name)).Distinct().Order().ToArray()});
  }
 }
 File.WriteAllText(args[1],JsonSerializer.Serialize(findings,new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine($"Compiled assembly audit: {audited} assemblies; {findings.Count} violations (including missing build outputs).");
 if(findings.Count>0)Environment.ExitCode=1;
 return;
}

foreach (var dir in Directory.EnumerateDirectories(args[0], "TomasAI.IFM.Framework.*"))
{
 var file = Path.Combine(dir,"debug",Path.GetFileName(dir)+".dll");
 if (!File.Exists(file)) continue;
 using var stream = File.OpenRead(file);
 using var pe = new PEReader(stream);
 var metadata = pe.GetMetadataReader();
 foreach (var handle in metadata.TypeReferences)
 {
  var type = metadata.GetTypeReference(handle);
  if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) continue;
  var reference = metadata.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
  var owner = metadata.GetString(reference.Name);
  if (!owner.StartsWith("TomasAI.IFM.Domain.",StringComparison.Ordinal) && !owner.StartsWith("TomasAI.IFM.Application.",StringComparison.Ordinal)) continue;
  output.Add(new { consumer=Path.GetFileName(dir),owner,type=metadata.GetString(type.Namespace)+"."+metadata.GetString(type.Name) });
 }
}
File.WriteAllText(args[1],JsonSerializer.Serialize(output,new JsonSerializerOptions { WriteIndented=true }));
Console.WriteLine($"Found {output.Count} upward type references in compiled Framework assemblies.");

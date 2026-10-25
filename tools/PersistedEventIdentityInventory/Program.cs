using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Shared.EventSourcing;
if (args.Length>0 && args[0]=="--qualify-stored")
{
 await StoredEventQualification.RunAsync(args[1],args[2],args.Length>3?args[3]:null);
 return;
}
// Composition tool: inspect compiled production contract closure before any assembly move.
var pending = new Queue<Assembly>();
pending.Enqueue(typeof(TomasAI.IFM.Application.Api.Server.Core.Hosting.PersistedEventTypeAliases).Assembly);
var seen = new HashSet<string>();
var entries = new List<object>();
var allTypes = new List<Type>();
while (pending.TryDequeue(out var assembly))
{
 if (!seen.Add(assembly.FullName!)) continue;
 foreach (var reference in assembly.GetReferencedAssemblies().Where(a => a.Name!.StartsWith("TomasAI.IFM.", StringComparison.Ordinal)))
  pending.Enqueue(Assembly.Load(reference));
 allTypes.AddRange(assembly.GetTypes());
 foreach (var type in assembly.GetTypes().Where(t => typeof(IEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters))
 {
  var identity = EventWriteIdentityRegistry.Default.GetIdentity(type);
  entries.Add(new { currentType = type.FullName, sourceAssembly = assembly.GetName().Name, identity.EventName, identity.EventTypeName });
 }
}
if (args[0] == "--qualify-public-ports")
{
 PublicPortQualification.Run(allTypes, args[1]);
 return;
}
if (args[0] == "--freeze-framework-types" || args[0] == "--freeze-all-events" || args[0] == "--freeze-wire-boundaries")
{
 FreezeContracts.Run(allTypes, args[1], args[2], args[0] == "--freeze-all-events", args[0] == "--freeze-wire-boundaries");
 return;
}
File.WriteAllText(args[0], JsonSerializer.Serialize(entries.OrderBy(e => JsonSerializer.Serialize(e), StringComparer.Ordinal), new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Inventoried {entries.Count} concrete event contracts across {seen.Count} production assemblies.");

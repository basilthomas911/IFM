using IdentityFixture.Relocated;
using TomasAI.IFM.Shared.EventSourcing;
var oldName = File.ReadAllText(args[0]+".type");
if (typeof(Observation).AssemblyQualifiedName == oldName) throw new Exception("The contract did not actually move.");
if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "IdentityFixture.Original"))
 throw new Exception("The relocated process must not load the original contract assembly.");
var reader = new EventTypeRegistry();
var writer = new EventWriteIdentityRegistry(reader);
writer.RegisterLegacyIdentity(typeof(Observation), "Observation", oldName);
reader.Register<Observation>("test.relocated-observation/v1");
var codec = new EventLogMessagePackCodec(bool.Parse(args[2]), reader);
var oldBytes = File.ReadAllBytes(args[0]);
foreach (var name in new[] { oldName, typeof(Observation).AssemblyQualifiedName!, "test.relocated-observation/v1" })
{
 var value = (Observation)codec.Deserialize(name,42,oldBytes);
 if (value.Value != 123.45m || value.EventId != 42) throw new Exception("Historical state mismatch.");
 var bytes = codec.Serialize(value with { EventId = 0 });
 if (!oldBytes.SequenceEqual(bytes)) throw new Exception("Payload bytes changed after relocation.");
 File.WriteAllBytes(args[1],bytes);
 File.WriteAllText(args[1]+".type",writer.GetIdentity(typeof(Observation)).EventTypeName);
}
if (File.ReadAllText(args[1]+".type") != oldName) throw new Exception("Write identity changed.");
return 0;

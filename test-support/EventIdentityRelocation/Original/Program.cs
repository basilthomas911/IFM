using IdentityFixture.Original;
using TomasAI.IFM.Shared.EventSourcing;
// Deliberately uses CLR lookup, bypassing the alias resolver: models the old reader.
if (args[0] == "write")
{
 var value = new Observation { Value = 123.45m };
 File.WriteAllText(args[1]+".type", typeof(Observation).AssemblyQualifiedName);
 File.WriteAllBytes(args[1], new EventLogMessagePackCodec(bool.Parse(args[2])).Serialize(value));
}
else
{
 var type = Type.GetType(File.ReadAllText(args[1]+".type"), throwOnError:true)!;
 var value = (Observation)new EventLogMessagePackCodec().Deserialize(type.AssemblyQualifiedName!, 42, File.ReadAllBytes(args[1]));
 if (value.Value != 123.45m || value.EventId != 42) return 2;
}
return 0;

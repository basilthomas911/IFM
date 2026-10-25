using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing;

static class FreezeContracts
{
 public static void Run(IEnumerable<Type> types,string references,string destination,bool allEvents=false,bool wireBoundaries=false)
 {
  using var document = JsonDocument.Parse(File.ReadAllText(references));
  var names=document.RootElement.EnumerateArray().Select(x=>x.GetProperty("type").GetString()!).ToHashSet(StringComparer.Ordinal);
  var closure=new HashSet<Type>();
  void Visit(Type type)
  {
   if(type.HasElementType) { Visit(type.GetElementType()!); return; }
   foreach(var argument in type.GetGenericArguments())Visit(argument);
   var owner=type.Assembly.GetName().Name!;
   if(!owner.StartsWith("TomasAI.IFM.Domain.",StringComparison.Ordinal) || !owner.EndsWith(".Shared",StringComparison.Ordinal) || !closure.Add(type))return;
   if(type.BaseType is {} parent)Visit(parent);
   foreach(var attribute in type.GetCustomAttributesData())
    foreach(var argument in attribute.ConstructorArguments)
     if(argument.Value is Type dependency)Visit(dependency);
   foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static))Visit(field.FieldType);
   foreach(var property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static))Visit(property.PropertyType);
   foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static))
   { Visit(method.ReturnType); foreach(var parameter in method.GetParameters())Visit(parameter.ParameterType); }
   foreach(var ctor in type.GetConstructors())foreach(var parameter in ctor.GetParameters())Visit(parameter.ParameterType);
  }
  if(wireBoundaries) closure.UnionWith(types.Where(t=>(t.IsPublic || t.IsNestedPublic) && (
    (t.Namespace?.StartsWith("TomasAI.IFM.Framework.MarketData.Contracts",StringComparison.Ordinal)==true && t.IsDefined(typeof(MessagePackObjectAttribute))) ||
    (t.Namespace=="TomasAI.IFM.Framework.OptionPricer.Pricing" && new[]{"CashDividend","DividendKind","ExerciseKind","ImpliedVolatilityResult","ImpliedVolatilityValues","OptionPriceDeltaValues","OptionPriceResult","OptionPricingRequest","OptionSide","OptionValues","PremiumKind","PriceDeltaResult","PricingFailure","PricingResult","PricingSettings","UnderlyingKind"}.Contains(t.Name)))));
  else if(allEvents) closure.UnionWith(types.Where(t=>typeof(IEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters));
  else foreach(var type in types.Where(t=>names.Contains(t.FullName!)))Visit(type);
  File.WriteAllText(destination+".types.json",JsonSerializer.Serialize(closure.OrderBy(t=>t.FullName,StringComparer.Ordinal).Select(t=>new {owner=t.Assembly.GetName().Name,type=t.FullName}),new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"Provider signature closure includes {closure.Count} types.");
  var entries=new List<object>();
  foreach (var type in closure.Where(t=>!t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters && !t.Name.EndsWith("Formatter",StringComparison.Ordinal)).OrderBy(t=>t.FullName,StringComparer.Ordinal))
  {
   foreach(var compressed in new[]{false,true})
   {
    Console.WriteLine($"Capturing {type.FullName}; LZ4={compressed}");
    var codec=new EventLogMessagePackCodec(compressed);
    var options=(MessagePackSerializerOptions)typeof(EventLogMessagePackCodec).GetField("_writeOptions",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(codec)!;
    var value=Create(type,new(),0)!;
    byte[] bytes;
    var isEvent=typeof(IEvent).IsAssignableFrom(type);
    if (isEvent) { TomasAI.IFM.Shared.EventModelActor.EventInitHelper.SetProperty((IEvent)value,nameof(IEvent.EventId),0L);bytes=codec.Serialize((IEvent)value); }
    else bytes=MessagePackSerializer.Serialize(type,value,options);
    entries.Add(new {fullName=type.FullName,legacyIdentity=isEvent?EventWriteIdentityRegistry.Default.GetIdentity(type).EventTypeName:type.AssemblyQualifiedName,compressed,isEvent,payload=Convert.ToBase64String(bytes),sha256=Convert.ToHexString(SHA256.HashData(bytes))});
   }
  }
  Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
  File.WriteAllText(destination,JsonSerializer.Serialize(entries,new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"Captured {entries.Count} frozen payloads before provider-contract moves.");
 }
 static object? Create(Type type,HashSet<Type> parents,int depth)
 {
  if(type==typeof(string))return "fixture";
  if(type.FullName=="TomasAI.IFM.Domain.MarketData.Shared.FuturesOptionContractId")return Activator.CreateInstance(type,"ES20261030P5000");
  if(type==typeof(Guid))return Guid.Parse("11111111-2222-3333-4444-555555555555");
  if(type==typeof(DateTime))return new DateTime(2026,10,25,12,0,0,DateTimeKind.Utc);
  if(type==typeof(DateTimeOffset))return new DateTimeOffset(2026,10,25,12,0,0,TimeSpan.Zero);
  if(type==typeof(DateOnly))return new DateOnly(2026,10,25);
  if(type==typeof(TimeOnly))return new TimeOnly(12,0);
  if(type==typeof(TimeSpan))return TimeSpan.FromSeconds(5);
  if(type==typeof(bool))return true;
  if(type.IsEnum)return Enum.GetValues(type).GetValue(0);
  if(type.IsPrimitive || type==typeof(decimal))return Convert.ChangeType(42,type,System.Globalization.CultureInfo.InvariantCulture);
  if(Nullable.GetUnderlyingType(type) is {} underlying)return Create(underlying,parents,depth);
  if(depth>4 || parents.Contains(type))return type.IsValueType?Activator.CreateInstance(type):null;
  if(type.FullName=="TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.FuturesTickQuoteDataSegment")
  {
   var ctor=type.GetConstructors().Single(c=>c.GetParameters().Length==2);
   return ctor.Invoke([Create(ctor.GetParameters()[0].ParameterType,parents,depth+1),(ushort)1]);
  }
  if(type.IsArray){var array=Array.CreateInstance(type.GetElementType()!,1);array.SetValue(Create(type.GetElementType()!,parents,depth+1),0);return array;}
  if(type.IsGenericType && type.GetGenericArguments().Length==1)
  {
   var element=type.GetGenericArguments()[0];var arrayType=element.MakeArrayType();
   if(type.GetGenericTypeDefinition()==typeof(System.Collections.Immutable.ImmutableArray<>))
   {
    var factory=typeof(System.Collections.Immutable.ImmutableArray).GetMethods().Single(m=>m.Name=="CreateRange" && m.IsGenericMethodDefinition && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType.IsGenericType && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition()==typeof(IEnumerable<>));
    return factory.MakeGenericMethod(element).Invoke(null,[Create(arrayType,parents,depth+1)]);
   }
   if(type.IsAssignableFrom(arrayType))return Create(arrayType,parents,depth+1);
   if(type.GetGenericTypeDefinition()==typeof(ReadOnlyMemory<>) || type.GetGenericTypeDefinition()==typeof(Memory<>) || type.GetGenericTypeDefinition()==typeof(ArraySegment<>))
    return Activator.CreateInstance(type,Create(arrayType,parents,depth+1));
  }
  if(type.IsAbstract || type.IsInterface || type.IsPointer || type.IsByRefLike)return null;
  var value=type.IsValueType?Activator.CreateInstance(type):RuntimeHelpers.GetUninitializedObject(type);
  parents.Add(type);
  foreach(var property in type.GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.SetMethod!=null && p.GetIndexParameters().Length==0))
   property.SetValue(value,Create(property.PropertyType,parents,depth+1));
  foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public).Where(f=>!f.IsLiteral))field.SetValue(value,Create(field.FieldType,parents,depth+1));
  parents.Remove(type);return value;
 }
}

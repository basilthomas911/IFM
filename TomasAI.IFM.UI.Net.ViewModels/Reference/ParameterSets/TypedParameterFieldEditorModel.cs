using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;

public static partial class ParameterFieldEditorModel
{
    public static ParameterField[] Read(ParameterSetValue value,bool includeIdentity=false)
    {
        var result=new List<ParameterField>();
        void Visit(object? node,Type type,string path)
        {
            type=Nullable.GetUnderlyingType(type)??type;
            if(node is null) {result.Add(new(path,"null",JsonValueKind.Null));return;}
            if(type==typeof(string)||type==typeof(Guid)||type==typeof(DateTime)||type==typeof(DateTimeOffset)||type==typeof(DateOnly)||type==typeof(TimeSpan))
            {result.Add(new(path,Text(node),JsonValueKind.String));return;}
            if(type==typeof(bool)){result.Add(new(path,(bool)node?"true":"false",(bool)node?JsonValueKind.True:JsonValueKind.False));return;}
            if(type.IsEnum||type.IsPrimitive||type==typeof(decimal))
            {result.Add(new(path,type.IsEnum?Convert.ToInt64(node,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture):Text(node),JsonValueKind.Number));return;}
            if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(ImmutableArray<>)&&(bool)type.GetProperty("IsDefault")!.GetValue(node)!)return;
            if(node is IEnumerable collection)
            {
                var element=type.IsArray?type.GetElementType()!:type.GetGenericArguments()[0];var index=0;
                foreach(var child in collection)Visit(child,element,path+"/"+index++);return;
            }
            foreach(var property in type.GetProperties().Where(x=>x.GetCustomAttribute<KeyAttribute>() is not null).OrderBy(x=>x.GetCustomAttribute<KeyAttribute>()!.IntKey))
            {
                if(!includeIdentity&&path.Length==0&&HiddenRoots.Contains(property.Name))continue;
                if(!includeIdentity&&property.Name=="IsRequired")continue;
                Visit(property.GetValue(node),property.PropertyType,path+"/"+property.Name);
            }
        }
        var property=value.GetType().GetProperty("Value")!;
        Visit(property.GetValue(value),property.PropertyType,"");return result.ToArray();
    }
    static string Text(object value)=>value switch
    {
        DateTime timestamp=>timestamp.ToString("O",CultureInfo.InvariantCulture),DateTimeOffset timestamp=>timestamp.ToString("O",CultureInfo.InvariantCulture),
        DateOnly date=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),TimeSpan span=>span.ToString("c",CultureInfo.InvariantCulture),
        IFormattable number=>number.ToString(null,CultureInfo.InvariantCulture),_=>value.ToString()??string.Empty
    };
    public static ParameterSetValue Apply(ParameterSetValue original,IEnumerable<ParameterField> changes)
    {
        var copy=ParameterValueCodec.Snapshot(original);var allowed=Read(copy).ToDictionary(x=>x.Path,StringComparer.Ordinal);
        var seen=new HashSet<string>(StringComparer.Ordinal);var valueProperty=copy.GetType().GetProperty("Value")!;
        foreach(var change in changes)
        {
            if(!seen.Add(change.Path)||!allowed.TryGetValue(change.Path,out var field)||field.Kind!=change.Kind)throw new ArgumentException("Invalid parameter field");
            Rewrite(valueProperty.GetValue(copy)!,valueProperty.PropertyType,Segments(change.Path),0,change);
        }
        return copy;
    }
    static object? Rewrite(object? node,Type type,string[] path,int index,ParameterField field)
    {
        if(index==path.Length)
        {
            if(field.Kind==JsonValueKind.Null)
            {if(field.Value!="null")throw new ArgumentException("Null fields cannot change type");return null;}
            type=Nullable.GetUnderlyingType(type)??type;
            if(type==typeof(string))return field.Value;
            if(type.IsEnum)return Enum.ToObject(type,Convert.ChangeType(field.Value,Enum.GetUnderlyingType(type),CultureInfo.InvariantCulture)!);
            if(type==typeof(Guid))return Guid.Parse(field.Value);
            if(type==typeof(DateTime))return DateTime.Parse(field.Value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
            if(type==typeof(DateTimeOffset))return DateTimeOffset.Parse(field.Value,CultureInfo.InvariantCulture);
            if(type==typeof(DateOnly))return DateOnly.ParseExact(field.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture);
            if(type==typeof(TimeSpan))return TimeSpan.ParseExact(field.Value,"c",CultureInfo.InvariantCulture);
            return Convert.ChangeType(field.Value,type,CultureInfo.InvariantCulture);
        }
        if(node is Array array)
        {
            var position=int.Parse(path[index],CultureInfo.InvariantCulture);var copy=(Array)array.Clone();
            copy.SetValue(Rewrite(copy.GetValue(position),type.GetElementType()!,path,index+1,field),position);return copy;
        }
        if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(ImmutableArray<>))
        {
            var element=type.GetGenericArguments()[0];var items=((IEnumerable)node!).Cast<object>().ToArray();var elements=Array.CreateInstance(element,items.Length);
            for(var i=0;i<items.Length;i++)elements.SetValue(items[i],i);
            var rewritten=Rewrite(elements,elements.GetType(),path,index,field);
            return typeof(ParameterFieldEditorModel).GetMethod(nameof(Immutable),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(element).Invoke(null,[rewritten]);
        }
        var property=type.GetProperty(path[index])??throw new ArgumentException("Unknown parameter field");
        property.SetValue(node,Rewrite(property.GetValue(node),property.PropertyType,path,index+1,field));return node;
    }
    static ImmutableArray<T> Immutable<T>(T[] values)=>ImmutableArray.CreateRange(values);
    public static string Display(ParameterSetValue value)=>string.Join(Environment.NewLine,Read(value,true).Select(x=>x.Path+": "+x.Value));
}

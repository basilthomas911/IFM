using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using MessagePack;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Encodes display documents directly as portable MessagePack maps, never JSON strings.</summary>
/// <remarks>Encoding uses generated property accessors; input binding is handled separately.</remarks>
public static class BridgeDocument
{
    static DisplayProperty[] Members(Type type)=>DisplayDescriptors.For(type);
    public static byte[] Encode(object? value,bool camelCase=false,bool enumNames=false,bool contractKeys=false)
    {
        using var buffer=new PooledBridgeBuffer();var writer=new MessagePackWriter(buffer);
        WriteDirect(ref writer,value,camelCase,enumNames,0,contractKeys);writer.Flush();return buffer.ToOwnedArray();
    }
    public static Dictionary<string,object?> Map(object value,bool camelCase=false,bool enumNames=false) => (Dictionary<string,object?>)Project(value,camelCase,enumNames)!;
    public static object? Project(object? value,bool camelCase=false,bool enumNames=false,int depth=0)
    {
        if(depth>64)throw new InvalidOperationException("Bridge display document exceeds maximum depth");
        if(value is null)return null;
        var type=value.GetType();
        if(type.IsEnum)return enumNames?value.ToString():Convert.ToInt64(value,CultureInfo.InvariantCulture);
        if(value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or byte[])return value;
        if(value is decimal amount)return amount.ToString(CultureInfo.InvariantCulture);
        if(value is Guid id)return id.ToString("D");
        if(value is DateTime date)return date.ToString("O",CultureInfo.InvariantCulture);
        if(value is DateTimeOffset offset)return offset.ToString("O",CultureInfo.InvariantCulture);
        if(value is DateOnly day)return day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        if(value is TimeOnly time)return time.ToString("O",CultureInfo.InvariantCulture);
        if(value is TimeSpan span)return span.ToString("c",CultureInfo.InvariantCulture);
        if(value is IDictionary dictionary){var map=new Dictionary<string,object?>();foreach(DictionaryEntry entry in dictionary)map[Convert.ToString(entry.Key,CultureInfo.InvariantCulture)!]=Project(entry.Value,camelCase,enumNames,depth+1);return map;}
        if(value is IEnumerable sequence)return sequence.Cast<object?>().Select(v=>Project(v,camelCase,enumNames,depth+1)).ToArray();
        return Members(type).ToDictionary(p=>camelCase?p.CamelName:p.Name,p=>Project(p.Read(value),camelCase,enumNames,depth+1));
    }
    /// <summary>Generated accessors keep primitive property values unboxed on the encoding path.</summary>
    public static void WriteTyped<T>(ref MessagePackWriter writer,T value,bool camelCase,bool enumNames,int depth,bool contractKeys)
    {
        if(typeof(T)==typeof(bool)){writer.Write(Unsafe.As<T,bool>(ref value));return;}
        if(typeof(T)==typeof(byte)){writer.Write(Unsafe.As<T,byte>(ref value));return;}
        if(typeof(T)==typeof(sbyte)){writer.Write(Unsafe.As<T,sbyte>(ref value));return;}
        if(typeof(T)==typeof(short)){writer.Write(Unsafe.As<T,short>(ref value));return;}
        if(typeof(T)==typeof(ushort)){writer.Write(Unsafe.As<T,ushort>(ref value));return;}
        if(typeof(T)==typeof(int)){writer.Write(Unsafe.As<T,int>(ref value));return;}
        if(typeof(T)==typeof(uint)){writer.Write(Unsafe.As<T,uint>(ref value));return;}
        if(typeof(T)==typeof(long)){writer.Write(Unsafe.As<T,long>(ref value));return;}
        if(typeof(T)==typeof(ulong)){writer.Write(Unsafe.As<T,ulong>(ref value));return;}
        if(typeof(T)==typeof(float)){writer.Write(Unsafe.As<T,float>(ref value));return;}
        if(typeof(T)==typeof(double)){writer.Write(Unsafe.As<T,double>(ref value));return;}
        if(typeof(T)==typeof(string)){writer.Write(Unsafe.As<T,string>(ref value));return;}
        WriteDirect(ref writer,value,camelCase,enumNames,depth,contractKeys);
    }
    static void WriteDirect(ref MessagePackWriter writer,object? value,bool camelCase,bool enumNames,int depth,bool contractKeys)
    {
        if(depth>64)throw new InvalidOperationException("Bridge display document exceeds maximum depth");
        if(value is null){writer.WriteNil();return;}
        var type=value.GetType();
        if(type.IsEnum){Write(ref writer,enumNames?value.ToString():Convert.ToInt64(value,CultureInfo.InvariantCulture));return;}
        if(value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or byte[]
            or decimal or Guid or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan) {
            Write(ref writer,Project(value,camelCase,enumNames,depth));return;
        }
        if(value is IDictionary map){writer.WriteMapHeader(map.Count);foreach(DictionaryEntry entry in map){writer.Write(Convert.ToString(entry.Key,CultureInfo.InvariantCulture)!);WriteDirect(ref writer,entry.Value,camelCase,enumNames,depth+1,contractKeys);}return;}
        if(value is IEnumerable sequence){
            if(value is ICollection collection){writer.WriteArrayHeader(collection.Count);foreach(var item in sequence)WriteDirect(ref writer,item,camelCase,enumNames,depth+1,contractKeys);}
            else {var items=sequence.Cast<object?>().ToArray();writer.WriteArrayHeader(items.Length);foreach(var item in items)WriteDirect(ref writer,item,camelCase,enumNames,depth+1,contractKeys);}
            return;
        }
        var members=Members(type);writer.WriteMapHeader(members.Length);
        foreach(var member in members){writer.Write(contractKeys?member.WireName:camelCase?member.CamelName:member.Name);member.Write(ref writer,value,camelCase,enumNames,depth+1,contractKeys);}
    }
    static void Write(ref MessagePackWriter w,object? value)
    {
        switch(value)
        {
            case null:w.WriteNil();break;
            case string v:w.Write(v);break;case bool v:w.Write(v);break;
            case byte v:w.Write(v);break;case sbyte v:w.Write(v);break;case short v:w.Write(v);break;case ushort v:w.Write(v);break;
            case int v:w.Write(v);break;case uint v:w.Write(v);break;case long v:w.Write(v);break;case ulong v:w.Write(v);break;
            case float v:w.Write(v);break;case double v:w.Write(v);break;case byte[] v:w.Write(v);break;
            case Dictionary<string,object?> map:w.WriteMapHeader(map.Count);foreach(var p in map){w.Write(p.Key);Write(ref w,p.Value);}break;
            case object?[] values:w.WriteArrayHeader(values.Length);foreach(var v in values)Write(ref w,v);break;
            default:throw new InvalidOperationException("Unsupported projected bridge value: "+value.GetType());
        }
    }
    public static T Decode<T>(ReadOnlyMemory<byte> bytes)=>BridgeInput.Decode<T>(bytes);
    public static Dictionary<string,string> Fields(object value,bool camelCase=false,bool enumNames=false) => Map(value,camelCase,enumNames).ToDictionary(p=>p.Key,p=>Display(p.Value));
    public static void Flatten(object value,Dictionary<string,string> fields,bool enumNames=false)
    {
        Visit(Project(value,false,enumNames),"");
        void Visit(object? v,string prefix){if(v is Dictionary<string,object?> map){foreach(var p in map)Visit(p.Value,prefix.Length==0?p.Key:prefix+"."+p.Key);}else if(v is not object?[])fields[prefix]=Display(v);}
    }
    public static string Display(object? value) => value switch
    {
        null=>"",Dictionary<string,object?> map=>"{ "+string.Join(", ",map.Select(p=>p.Key+": "+Display(p.Value)))+" }",
        object?[] values=>"[ "+string.Join(", ",values.Select(Display))+" ]",IFormattable v=>v.ToString(null,CultureInfo.InvariantCulture),_=>value.ToString()??""
    };
}

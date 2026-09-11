using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;

public sealed record ParameterField(string Path,string Value,JsonValueKind Kind)
{
    public string Group => ParameterFieldEditorModel.GroupName(Path);
    public string Name => ParameterFieldEditorModel.DisplayName(Path);
}

/// <summary>Edits and groups scalar configuration fields without exposing executable schema expressions.</summary>
public static class ParameterFieldEditorModel
{
    static readonly HashSet<string> HiddenRoots = ["ParameterSetId","Version","SchemaVersion","StrategyParameterSetId","StrategyParameterSetVersion","SignalRequirements","SignalMetrics","ObservationMetrics","Horizon","TargetHorizon"];
    public static ParameterField[] Read(string json)
    {
        using var document=JsonDocument.Parse(json);var fields=new List<ParameterField>();
        void Visit(JsonElement value,string path)
        {
            if(value.ValueKind==JsonValueKind.Object)
                foreach(var property in value.EnumerateObject())
                {if(path.Length==0&&HiddenRoots.Contains(property.Name))continue;Visit(property.Value,path+"/"+property.Name.Replace("~","~0").Replace("/","~1"));}
            else if(value.ValueKind==JsonValueKind.Array)
            {var i=0;foreach(var item in value.EnumerateArray())Visit(item,path+"/"+i++);}
            else if(!path.EndsWith("/IsRequired",StringComparison.Ordinal)) fields.Add(new(path,value.ValueKind==JsonValueKind.String?value.GetString()!:value.GetRawText(),value.ValueKind));
        }
        Visit(document.RootElement,"");return fields.ToArray();
    }
    public static string[] Groups(IEnumerable<ParameterField> fields) => fields
        .Select(field=>field.Group).Where(group=>group.Length!=0).Distinct(StringComparer.Ordinal).ToArray();
    public static string GroupName(string path)
    {
        var segments=Segments(path);
        return segments.Length switch {0=>string.Empty,1=>"General",_=>Humanize(segments[0])};
    }
    public static string DisplayName(string path)
    {
        var pathSegments=Segments(path);
        var segments=(pathSegments.Length==1?pathSegments:pathSegments.Skip(1)).ToArray();
        if(segments.Length==0)return string.Empty;
        var result="";
        foreach(var segment in segments)
            result+=int.TryParse(segment,NumberStyles.None,CultureInfo.InvariantCulture,out _)
                ?$" [{segment}]"
                :(result.Length==0?Humanize(segment):" / "+Humanize(segment));
        return result;
    }
    public static string Apply(string original,IEnumerable<ParameterField> changes)
    {
        var allowed=Read(original).ToDictionary(x=>x.Path,StringComparer.Ordinal);var root=JsonNode.Parse(original)!;var seen=new HashSet<string>();
        foreach(var change in changes)
        {
            if(!seen.Add(change.Path)||!allowed.TryGetValue(change.Path,out var field)||field.Kind!=change.Kind)
                throw new ArgumentException("Invalid parameter field.");
            JsonNode? replacement;
            if(field.Kind==JsonValueKind.String)replacement=JsonValue.Create(change.Value);
            else
            {
                using var parsed=JsonDocument.Parse(change.Value);
                if(parsed.RootElement.ValueKind!=field.Kind && !(field.Kind is JsonValueKind.True or JsonValueKind.False && parsed.RootElement.ValueKind is JsonValueKind.True or JsonValueKind.False))
                    throw new ArgumentException($"Invalid value type for {change.Path}.");
                replacement=JsonNode.Parse(change.Value);
            }
            var parts=change.Path.Split('/').Skip(1).Select(x=>x.Replace("~1","/").Replace("~0","~")).ToArray();var parent=root;
            foreach(var part in parts.SkipLast(1))parent=parent is JsonArray array?array[int.Parse(part,CultureInfo.InvariantCulture)]!:parent[part]!;
            if(parent is JsonArray target)target[int.Parse(parts[^1],CultureInfo.InvariantCulture)]=replacement;
            else parent[parts[^1]]=replacement;
        }
        return root.ToJsonString();
    }
    static string[] Segments(string path)=>path.Split('/',StringSplitOptions.RemoveEmptyEntries)
        .Select(value=>value.Replace("~1","/").Replace("~0","~")).ToArray();
    static string Humanize(string value)=>Regex.Replace(value.Replace('_',' '),"(?<=[a-z0-9])(?=[A-Z])"," ");
}
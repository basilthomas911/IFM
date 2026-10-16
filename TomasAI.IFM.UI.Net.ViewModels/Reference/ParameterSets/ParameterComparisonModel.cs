using System.Text.Json;
namespace TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;

public sealed record ParameterDifference(string Path, string Before, string After);
public static class ParameterComparisonModel
{
    public static ParameterDifference[] Compare(TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterSetValue before,TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterSetValue after)
    {
        var a=ParameterFieldEditorModel.Read(before,true).ToDictionary(x=>x.Path,x=>x.Value,StringComparer.Ordinal);
        var b=ParameterFieldEditorModel.Read(after,true).ToDictionary(x=>x.Path,x=>x.Value,StringComparer.Ordinal);
        return a.Keys.Concat(b.Keys).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal)
            .Where(key=>a.GetValueOrDefault(key,"(absent)")!=b.GetValueOrDefault(key,"(absent)"))
            .Select(key=>new ParameterDifference(key,a.GetValueOrDefault(key,"(absent)"),b.GetValueOrDefault(key,"(absent)"))).ToArray();
    }

    public static ParameterDifference[] Compare(string before, string after)
    {
        using var left = JsonDocument.Parse(before); using var right = JsonDocument.Parse(after); var result = new List<ParameterDifference>();
        void Visit(JsonElement a, JsonElement b, string path)
        {
            if (a.ValueKind == JsonValueKind.Object && b.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in a.EnumerateObject().Select(x => x.Name).Concat(b.EnumerateObject().Select(x => x.Name)).Distinct().OrderBy(x => x, StringComparer.Ordinal))
                { a.TryGetProperty(key, out var av); b.TryGetProperty(key, out var bv); Visit(av, bv, path + "/" + key.Replace("~", "~0").Replace("/", "~1")); }
            }
            else if (a.ValueKind == JsonValueKind.Array && b.ValueKind == JsonValueKind.Array)
            { for (var i = 0; i < Math.Max(a.GetArrayLength(), b.GetArrayLength()); i++) Visit(i < a.GetArrayLength() ? a[i] : default, i < b.GetArrayLength() ? b[i] : default, path + "/" + i); }
            else
            {
                var av = a.ValueKind == JsonValueKind.Undefined ? "(absent)" : a.GetRawText(); var bv = b.ValueKind == JsonValueKind.Undefined ? "(absent)" : b.GetRawText();
                if (av != bv) result.Add(new(path, av, bv));
            }
        }
        Visit(left.RootElement, right.RootElement, ""); return result.ToArray();
    }
}

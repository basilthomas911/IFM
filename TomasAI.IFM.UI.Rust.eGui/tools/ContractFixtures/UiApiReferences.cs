using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Resolved source references, not runtime reachability claims. A
/// service method can exist without being called by the current Rust screen.</summary>
internal static class UiApiReferences
{
    public static Dictionary<(string Client,string Method,int Arity),string[]> Scan(string repository)
    {
        var roots=new[]{"TomasAI.IFM.UI.Net.Services","TomasAI.IFM.UI.Net.ViewModels",
            "TomasAI.IFM.UI.Net.Models","TomasAI.IFM.UI.Rust.Bridge.Net"};
        var trees=roots.SelectMany(root=>Directory.EnumerateFiles(Path.Combine(repository,root),"*.cs",SearchOption.AllDirectories))
            .Where(path=>!path.Contains("\\obj\\",StringComparison.OrdinalIgnoreCase)&&!path.Contains("\\bin\\",StringComparison.OrdinalIgnoreCase))
            .Select(path=>CSharpSyntaxTree.ParseText(File.ReadAllText(path),path:path)).ToArray();
        var references=new List<MetadataReference>();
        foreach(var path in Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll"))
            try{references.Add(MetadataReference.CreateFromFile(path));}catch(BadImageFormatException){ }
        // Runtime references bind BCL names; application references bind the
        // existing strongly typed API interfaces without executing a service.
        foreach(var path in ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))
            if(!references.Any(reference=>reference.Display==path))references.Add(MetadataReference.CreateFromFile(path));
        var implicitUsings=CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.Linq; global using System.Threading; global using System.Threading.Tasks; global using System.IO; global using System.Net.Http;");
        var compilation=CSharpCompilation.Create("UiApiReferenceInventory",trees.Append(implicitUsings),references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var found=new Dictionary<(string,string,int),HashSet<string>>();
        foreach(var tree in trees){
            var model=compilation.GetSemanticModel(tree);
            foreach(var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()){
                if(model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)continue;
                var type=method.ContainingType;
                if(type is null||!type.Name.EndsWith("Api",StringComparison.Ordinal)
                    ||!type.ContainingNamespace.ToDisplayString().StartsWith("TomasAI.IFM.",StringComparison.Ordinal))continue;
                var client=type.TypeKind==TypeKind.Interface&&type.Name.StartsWith('I')?type.Name[1..]:type.Name;
                var key=(client,method.Name,method.Parameters.Length);
                if(!found.TryGetValue(key,out var sites))found[key]=sites=[];
                sites.Add(Path.GetRelativePath(repository,tree.FilePath).Replace('\\','/')+":"+(tree.GetLineSpan(call.Span).StartLinePosition.Line+1));
            }
        }
        return found.ToDictionary(row=>row.Key,row=>row.Value.Order(StringComparer.Ordinal).ToArray());
    }
}

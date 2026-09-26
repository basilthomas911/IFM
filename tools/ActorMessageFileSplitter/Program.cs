using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

// Syntax-aware, mechanical source-file layout migration. Contract declarations are never rewritten.
var repository = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory());
var apply = args.Contains("--apply", StringComparer.Ordinal);
var audit = args.Contains("--audit", StringComparer.Ordinal);
var only = args.FirstOrDefault(a => a.StartsWith("--only=", StringComparison.Ordinal))?[7..];
var changed = 0;
var created = 0;
var skipped = new List<string>();
var manifest = new List<string>();
foreach (var file in Directory.EnumerateFiles(repository, "*.cs", SearchOption.AllDirectories)
             .Where(IsDomainSharedSource)
             .Where(p => only is null || p.Contains(only, StringComparison.OrdinalIgnoreCase))
             .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
{
    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source);
    var root = tree.GetCompilationUnitRoot();
    if (root.ContainsDiagnostics) { skipped.Add($"PARSE {file}"); continue; }
    var containers = root.DescendantNodesAndSelf().OfType<BaseNamespaceDeclarationSyntax>()
        .Where(n => !n.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Any()).ToArray();
    if (containers.Length != 1 || root.Members.Count != 1 || root.Members[0] != containers[0])
    {
        var candidates = root.DescendantNodes().OfType<TypeDeclarationSyntax>().Count(IsMessage);
        if (candidates > 1) skipped.Add($"STRUCTURE {file}");
        continue;
    }
    var ns = containers[0];
    var types = ns.Members.OfType<TypeDeclarationSyntax>().Where(IsMessage).ToArray();
    var groups = types.GroupBy(t => Family(t.Identifier.ValueText)).ToArray();
    if (groups.Length <= 1) continue;
    var keep = groups.FirstOrDefault(g => Path.GetFileNameWithoutExtension(file).Equals(g.Key, StringComparison.Ordinal))
        ?? groups.FirstOrDefault(g => g.Any(t => t.Identifier.ValueText.Equals(Path.GetFileNameWithoutExtension(file), StringComparison.Ordinal)));
    if (keep is null && ns.Members.Any(m => m is not TypeDeclarationSyntax t || !IsMessage(t)))
        keep = null;
    var moves = groups.Where(g => g != keep).ToArray();
    var plans = new List<(string target, string content, TypeDeclarationSyntax[] types)>();
    var header = source[..ns.SpanStart];
    var namespaceOpen = source[ns.SpanStart..ns.Members[0].FullSpan.Start];
    var namespaceClose = ns is NamespaceDeclarationSyntax ? source[ns.Members.Last().FullSpan.End..ns.Span.End] : "";
    foreach (var group in moves)
    {
        var target = Path.Combine(Path.GetDirectoryName(file)!, group.Key + ".cs");
        if (File.Exists(target)) { skipped.Add($"COLLISION {file} -> {target}"); continue; }
        var body = string.Concat(group.Select(t => t.ToFullString()));
        var content = header + namespaceOpen + body + namespaceClose + Environment.NewLine;
        plans.Add((target, content, group.ToArray()));
    }
    if (plans.Count == 0) continue;
    var removed = plans.SelectMany(p => p.types).ToHashSet();
    var remaining = ns.Members.Where(m => m is not TypeDeclarationSyntax t || !removed.Contains(t)).ToArray();
    var rewritten = root.RemoveNodes(removed, SyntaxRemoveOptions.KeepNoTrivia)?.ToFullString() ?? source;
    if (apply)
    {
        foreach (var plan in plans) File.WriteAllText(plan.target, plan.content, new UTF8Encoding(false));
        if (remaining.Length == 0) File.Delete(file);
        else File.WriteAllText(file, rewritten, new UTF8Encoding(false));
    }
    changed++;
    created += plans.Count;
    manifest.Add($"{Path.GetRelativePath(repository, file)} -> {string.Join(", ", plans.Select(p => Path.GetFileName(p.target)))}");
}
Console.WriteLine($"Mode={(apply ? "apply" : "dry-run")} source-files={changed} new-files={created} skipped={skipped.Count}");
foreach (var entry in manifest) Console.WriteLine(entry);
foreach (var entry in skipped) Console.Error.WriteLine(entry);
if (skipped.Count != 0) Environment.ExitCode = 2;
else if (audit && changed != 0) Environment.ExitCode = 1;

static bool IsDomainSharedSource(string file)
{
    if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) return false;
    return file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(part => part.StartsWith("TomasAI.IFM.Domain.", StringComparison.Ordinal) && part.EndsWith(".Shared", StringComparison.Ordinal));
}

static bool IsMessage(TypeDeclarationSyntax type)
{
    if (type is not ClassDeclarationSyntax && type is not RecordDeclarationSyntax && type is not StructDeclarationSyntax) return false;
    var name = type.Identifier.ValueText;
    return (name.EndsWith("Command", StringComparison.Ordinal) ||
            name.EndsWith("Query", StringComparison.Ordinal) ||
            name.EndsWith("Event", StringComparison.Ordinal))
           && !type.Modifiers.Any(SyntaxKind.AbstractKeyword);
}

static string Family(string name)
{
    if (!name.EndsWith("Event", StringComparison.Ordinal)) return name;
    var stem = name[..^"Event".Length];
    if (stem.EndsWith("CompleteApi", StringComparison.Ordinal)) return stem[..^"CompleteApi".Length] + "ApiEvent";
    if (stem.EndsWith("FailApi", StringComparison.Ordinal)) return stem[..^"FailApi".Length] + "ApiEvent";
    if (stem.EndsWith("Complete", StringComparison.Ordinal)) return stem[..^"Complete".Length] + "Event";
    if (stem.EndsWith("Fail", StringComparison.Ordinal)) return stem[..^"Fail".Length] + "Event";
    return name;
}

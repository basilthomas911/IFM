using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Parse syntax rather than text so comments, strings and normal 'this' expressions are ignored.
var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var options = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols:
    ["DEBUG", "TRACE", "WINDOWS", "NET10_0", "NET10_0_OR_GREATER", "NET9_0_OR_GREATER",
     "NET8_0_OR_GREATER", "NET7_0_OR_GREATER", "NET6_0_OR_GREATER", "NET5_0_OR_GREATER"]);
if (args.Contains("--self-test", StringComparer.Ordinal))
{
    Check("static class X { public static int M(this string s) => s.Length; }", 1);
    Check("static class X { extension(string s) { public int M() => s.Length; } }", 0);
    Check("class X { string M() => \"this string s\"; /* this int x */ }", 0);
    Check("static class X {\n#if NET6_0_OR_GREATER\npublic static int M(this string s) => s.Length;\n#endif\n}", 1);
    Console.WriteLine("Extension convention self-tests passed.");
    return 0;
}

var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false };
foreach (var argument in new[] { "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", "*.cs" })
    start.ArgumentList.Add(argument);
using var git = Process.Start(start) ?? throw new InvalidOperationException("Cannot start git.");
var paths = git.StandardOutput.ReadToEnd().Split('\0', StringSplitOptions.RemoveEmptyEntries);
git.WaitForExit();
if (git.ExitCode != 0) return git.ExitCode;
var failures = 0;
var scanned = 0;
foreach (var relative in paths.Distinct(StringComparer.OrdinalIgnoreCase))
{
    // The bundled upstream IBKR SDK deliberately keeps its original syntax and framework support.
    if (relative.StartsWith("TWS API/", StringComparison.OrdinalIgnoreCase)) continue;
    var path = Path.Combine(root, relative);
    if (!File.Exists(path)) continue;
    scanned++;
    var source = File.ReadAllText(path);
    // Also parse without framework symbols to inspect alternate conditional branches.
    var violations = Find(source, options).Concat(Find(source, options.WithPreprocessorSymbols(Array.Empty<string>())))
        .DistinctBy(method => method.SpanStart);
    foreach (var method in violations)
    {
        var line = method.SyntaxTree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
        Console.Error.WriteLine($"{relative}:{line}: use a C# 14 extension block for {method.Identifier.ValueText}.");
        failures++;
    }
}
Console.WriteLine($"Checked {scanned} first-party C# files; {failures} legacy extension declarations.");
return failures == 0 ? 0 : 1;

static IEnumerable<MethodDeclarationSyntax> Find(string source, CSharpParseOptions parseOptions) =>
    CSharpSyntaxTree.ParseText(source, parseOptions).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
        .Where(method => method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true);

void Check(string source, int expected)
{
    var actual = Find(source, options).Count();
    if (actual != expected) throw new InvalidOperationException($"Expected {expected} violations, got {actual}.");
}

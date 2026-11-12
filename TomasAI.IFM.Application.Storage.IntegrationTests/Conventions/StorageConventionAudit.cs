using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.Conventions;

internal sealed record StorageContextSpec(string Name, string Context)
{
    internal string Stem => Context[..^"DbContext".Length];
    internal string Combined => "I" + Context;
    internal string Read => "I" + Context.Replace("DbContext", "DbReadContext", StringComparison.Ordinal);
    internal string Write => "I" + Context.Replace("DbContext", "DbWriteContext", StringComparison.Ordinal);
    internal string Folder => Path.Combine(DbContextConventionSource.RepositoryRoot, "TomasAI.IFM.Application.Storage", Name);
    internal string RuntimeCatalog => Stem + "DbSql.cs";
    internal string Parameters => Stem + "DbParameters.cs";
    internal string SchemaCatalog => Stem + "SchemaSql.cs";
    internal string SchemaExecutor => Stem + "SchemaDb.cs";
    internal string Extensions => Context + "Extensions.cs";
}

internal sealed record StorageSource(string Path, CompilationUnitSyntax Root)
{
    internal static StorageSource Parse(string path, string text) => new(path,
        CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview), path).GetCompilationUnitRoot());
}

/// <summary>Reports every occurrence; ownership rules include supporting files rather than only the context class.</summary>
internal static class StorageConventionAudit
{
    internal static readonly StorageContextSpec[] Contexts =
    [
        new("ConfigurationDb", "ConfigurationDbContext"),
        new("EventSourceDb", "EventSourceActorDbContext"),
        new("MarketDataDb", "MarketDataDbContext"),
        new("MarketDataServiceDb", "MarketDataServiceDbContext"),
        new("PortfolioDb", "PortfolioDbContext"),
        new("ReferenceDb", "ReferenceDbContext"),
        new("ScheduledTaskDb", "ScheduledTaskDbContext"),
        new("SecuritiesDb", "SecuritiesDbContext"),
        new("SequenceIdDb", "SequenceIdDbContext"),
        new("SystemAdminDb", "SystemAdminDbContext"),
        new("TradeDb", "TradeDbContext"),
        new("TradePlanDb", "TradePlanDbContext"),
        new("TradeTickQuoteDb", "TradeTickQuoteDbContext")
    ];

    internal static readonly string[] Rules = ["Layout", "Structure", "Capabilities", "ContractMethods", "Helpers", "Statements",
        "Parameters", "Dialect", "Schema", "SchemaSqlFormat", "RuntimeSqlFormat", "Validation", "Ownership", "QueryResults", "Mappers", "Documentation", "FluentCalls"];

    static readonly Lazy<Dictionary<string, StorageSource[]>> Sources = new(() => Contexts.ToDictionary(spec => spec.Name,
        spec => Directory.GetFiles(spec.Folder, "*.cs", SearchOption.AllDirectories)
            .Select(path => StorageSource.Parse(path, File.ReadAllText(path))).ToArray(), StringComparer.Ordinal));

    internal static IReadOnlyList<string> Inspect(StorageContextSpec spec, string rule)
        => rule == "Layout"
            ? InspectLayout(spec,
                Directory.EnumerateFiles(spec.Folder, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(spec.Folder, path)),
                Directory.EnumerateDirectories(spec.Folder, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(spec.Folder, path)))
            : Inspect(spec, rule, Sources.Value[spec.Name]);

    internal static IReadOnlyList<string> InspectLayout(StorageContextSpec spec, IEnumerable<string> files, IEnumerable<string> directories)
    {
        string[] expected =
        [
            spec.Context + ".cs", spec.Extensions, spec.Parameters, spec.RuntimeCatalog,
            spec.Combined + ".cs", spec.Read + ".cs", spec.Write + ".cs",
            "Schema/" + spec.SchemaExecutor, "Schema/" + spec.SchemaCatalog
        ];
        var actual = files.Select(path => path.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        var folders = directories.Select(path => path.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        var findings = new List<string>();
        findings.AddRange(expected.Except(actual, StringComparer.Ordinal).Select(path => $"{spec.Name}/Layout: missing file {path}"));
        findings.AddRange(actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(path => $"{spec.Name}/Layout: unexpected file {path}"));
        if (!folders.Remove("Schema"))
            findings.Add($"{spec.Name}/Layout: missing directory Schema");
        findings.AddRange(folders.Order(StringComparer.Ordinal).Select(path => $"{spec.Name}/Layout: unexpected directory {path}"));
        return findings;
    }

    internal static IReadOnlyList<string> InspectSchemaSqlFormat(StorageSource source) =>
        InspectSqlCatalogFormat(source, schema: true);

    internal static IReadOnlyList<string> InspectRuntimeSqlFormat(StorageSource source) =>
        InspectSqlCatalogFormat(source, schema: false);

    static IReadOnlyList<string> InspectSqlCatalogFormat(StorageSource source, bool schema)
    {
        var label = schema ? "Schema SQL" : "Runtime SQL";
        var findings = new List<string>();
        void Add(SyntaxNode node, string message)
            => findings.Add($"{source.Path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {message}");

        var declarations = source.Root.DescendantNodes().OfType<MemberDeclarationSyntax>()
            .Where(member => member is not BaseNamespaceDeclarationSyntax
                && member.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .ToArray();
        if (declarations is not [ClassDeclarationSyntax catalog]
            || catalog.Identifier.ValueText != Path.GetFileNameWithoutExtension(source.Path)
            || catalog.Modifiers.Count != 2 || !catalog.Modifiers.Any(SyntaxKind.PublicKeyword)
            || !catalog.Modifiers.Any(SyntaxKind.StaticKeyword) || catalog.BaseList is not null
            || catalog.TypeParameterList is not null)
        {
            Add(source.Root, $"{label} files must declare exactly one public static catalog class.");
            return findings;
        }

        if (catalog.Members.Count == 0)
            Add(catalog, $"{label} catalogs must contain SQL constants.");

        string? previousName = null;
        foreach (var member in catalog.Members)
        {
            if (member is not FieldDeclarationSyntax field || field.Modifiers.Count != 2
                || !field.Modifiers.Any(SyntaxKind.InternalKeyword) || !field.Modifiers.Any(SyntaxKind.ConstKeyword)
                || field.Declaration.Type is not PredefinedTypeSyntax type || !type.Keyword.IsKind(SyntaxKind.StringKeyword)
                || field.Declaration.Variables is not [var variable]
                || variable.Initializer?.Value is not LiteralExpressionSyntax literal
                || !literal.Token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
            {
                Add(member, $"{label} members must each be an internal const string with one direct multiline raw literal.");
                continue;
            }
            var name = variable.Identifier.ValueText;
            if (previousName is not null && string.CompareOrdinal(previousName, name) >= 0)
                Add(field, $"Order SQL constants alphabetically by identifier. {name} follows {previousName}.");
            previousName = name;

            if (!Regex.IsMatch(Documentation(field), @"<summary>\s*[^\s<][\s\S]*?</summary>", RegexOptions.CultureInvariant))
                Add(field, $"{label} constants require a short XML summary.");

            var lines = literal.Token.Text.ReplaceLineEndings("\n").Split('\n');
            var rawSpan = literal.GetLocation().GetLineSpan();
            if (lines[0] != "\"\"\"" || lines[^1].Trim() != "\"\"\""
                || variable.Initializer.EqualsToken.GetLocation().GetLineSpan().StartLinePosition.Line >= rawSpan.StartLinePosition.Line
                || field.SemicolonToken.GetLocation().GetLineSpan().StartLinePosition.Line != rawSpan.EndLinePosition.Line)
                Add(literal, "Place the opening triple quotes and closing triple quotes with the C# semicolon on their own lines.");

            var sql = literal.Token.ValueText;
            var isStatement = schema
                ? LooksLikeSql(sql) || Regex.IsMatch(sql, @"^\s*(DO|COMMENT|GRANT|REVOKE|SET|RESET)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                : Regex.IsMatch(sql, @"^\s*(SELECT|WITH|INSERT|UPDATE|DELETE|SET|SAVEPOINT|ROLLBACK|LOCK|TRUNCATE|BEGIN|RELEASE)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!isStatement)
                Add(literal, $"{label} constants must contain a database statement or template.");
            if (!sql.TrimEnd().EndsWith(';'))
                Add(literal, "The final SQL statement must end with a semicolon.");
            if (schema)
            {
                foreach (var table in MisformattedSchemaFields(sql))
                    Add(literal, $"Put each field or constraint of {table} on its own line, with commas between entries and the closing parenthesis on its own line.");
            }
            else
            {
                foreach (var token in MisformattedRuntimeSqlTokens(sql))
                    Add(literal, $"Use uppercase SQL keywords and lowercase unquoted identifiers. Check '{token}'.");
            }
        }
        return findings;
    }

    static readonly HashSet<string> RuntimeSqlKeywords = new(
        ("ALL AND AS ASC BETWEEN BY CASE CAST COALESCE COLLATE CONFLICT CROSS DELETE DESC DISTINCT DO ELSE END EXCLUSIVE " +
         "EXISTS FALSE FETCH FOR FROM GROUP HAVING IN INNER INSERT INTERSECT INTO IS JOIN LATERAL LEFT LIKE LIMIT LOCAL LOCK " +
         "MAX MIN MODE NOT NOTHING NULL OFFSET ON OR ORDER OUTER OVERRIDING RECURSIVE RETURNING RIGHT ROLLBACK ROW SAVEPOINT " +
         "SELECT SET SHARE SYSTEM TABLE THEN TO TRUE UNION UPDATE USING VALUE VALUES WHEN WHERE WITH " +
         "ALLOW ANY APPLY ARRAY AVG BATCH COUNT CURRENT_TIMESTAMP EXCEPT EXCLUDED FILTER FILTERING FOLLOWING GREATEST INTERVAL LEAST LOCKED MATERIALIZED NOWAIT NULLIF " +
         "OF ONLY OVER PARTITION PRECEDING RANGE RELEASE ROWS SIMILAR SKIP SUM TRUNCATE UNBOUNDED UNLOGGED").Split(' '),
        StringComparer.OrdinalIgnoreCase);

    static IEnumerable<string> MisformattedRuntimeSqlTokens(string sql)
    {
        // Quoted data, quoted identifiers, positional binds and template holes retain their spelling.
        var tokens = Regex.Matches(sql, """"
            '(?:''|[^'])*'|"(?:""|[^"])*"|--[^\r\n]*|/\*[\s\S]*?\*/|\$\d+|\{\d+\}|(?<!:):[A-Za-z_]\w*|(?<word>[A-Za-z_][A-Za-z_0-9]*)
            """", RegexOptions.CultureInvariant);
        return tokens.Select(token => token.Groups["word"].Value)
            .Where(word => word.Length > 0 && (RuntimeSqlKeywords.Contains(word)
                ? word != word.ToUpperInvariant()
                : word != word.ToLowerInvariant()))
            .Distinct(StringComparer.Ordinal);
    }

    static IEnumerable<string> MisformattedSchemaFields(string sql)
    {
        var text = sql.ReplaceLineEndings("\n");
        var tokens = new Regex(""""'(?:''|[^'])*'|"(?:""|[^"])*"|[(),]"""", RegexOptions.CultureInvariant);
        foreach (Match table in Regex.Matches(text, @"\bCREATE\s+(?:TABLE|TYPE)\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>[\w.]+)\s*\(",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var start = table.Index + table.Length;
            var depth = 0;
            foreach (Match token in tokens.Matches(text, start))
            {
                if (depth == 0 && token.Value is "," or ")")
                {
                    var entry = text[start..token.Index];
                    var trimmed = entry.Trim();
                    var startsOnNewLine = entry[..(entry.Length - entry.TrimStart().Length)].Contains('\n');
                    var endsOnNewLine = entry[entry.TrimEnd().Length..].Contains('\n');
                    if (trimmed.Length == 0 || trimmed.Contains('\n') || !startsOnNewLine || endsOnNewLine != (token.Value == ")"))
                    {
                        yield return table.Groups["name"].Value;
                        break;
                    }
                    if (token.Value == ")") break;
                    start = token.Index + token.Length;
                }
                else if (token.Value == "(")
                    depth++;
                else if (token.Value == ")")
                    depth--;
            }
        }
    }

    internal static IReadOnlyList<string> Inspect(StorageContextSpec spec, string rule, StorageSource[] sources)
    {
        var errors = new List<string>();
        void Add(StorageSource source, SyntaxNode node, string reason) => errors.Add(
            $"{spec.Name}/{rule}: {source.Path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {reason}");
        if (rule is "SchemaSqlFormat" or "RuntimeSqlFormat")
        {
            var schema = rule == "SchemaSqlFormat";
            var filename = schema ? spec.SchemaCatalog : spec.RuntimeCatalog;
            var catalog = sources.SingleOrDefault(source => Path.GetFileName(source.Path) == filename);
            return catalog is null
                ? [$"{spec.Name}/{rule}: missing {filename}."]
                : InspectSqlCatalogFormat(catalog, schema);
        }

        var context = sources.SelectMany(source => source.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText == spec.Context).Select(type => (Source: source, Type: type))).ToArray();
        if (rule == "Structure")
        {
            if (context.Length != 1) errors.Add($"{spec.Name}/Structure: Expected exactly one non-partial context declaration; found {context.Length}.");
            foreach (var (source, type) in context)
            {
                if (type.Modifiers.Any(SyntaxKind.PartialKeyword)) Add(source, type, "Context must not be partial.");
                var bases = type.BaseList?.Types.Select(item => Compact(item.Type.ToString())).ToArray() ?? [];
                if (bases.Length != 2 || !bases[0].EndsWith($"ObjectDataRepository<{spec.Context}>", StringComparison.Ordinal)
                    || !bases[1].EndsWith(spec.Combined, StringComparison.Ordinal))
                    Add(source, type, "Direct bases must be ObjectDataRepository<Self> and only the combined persistence contract.");
            }
        }
        if (rule == "Capabilities")
        {
            foreach (var name in new[] { spec.Combined, spec.Read, spec.Write })
                if (!sources.SelectMany(source => source.Root.DescendantNodes().OfType<InterfaceDeclarationSyntax>()).Any(type => type.Identifier.ValueText == name))
                    errors.Add($"{spec.Name}/Capabilities: Missing {name}.");
            foreach (var (source, type) in context)
                foreach (var name in new[] { "Database", "DbReader", "DbWriter" })
                {
                    var property = type.Members.OfType<PropertyDeclarationSyntax>().SingleOrDefault(item => item.Identifier.ValueText == name);
                    if (property?.ExpressionBody?.Expression is not ThisExpressionSyntax) Add(source, property ?? (SyntaxNode)type, $"{name} must return this.");
                }
            foreach (var source in sources)
                foreach (var combined in source.Root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Where(type => type.Identifier.ValueText == spec.Combined))
                {
                    var bases = combined.BaseList?.Types.Select(item => item.Type.ToString()).ToArray() ?? [];
                    foreach (var name in new[] { spec.Read, spec.Write })
                        if (!bases.Any(value => value.EndsWith(name, StringComparison.Ordinal))) Add(source, combined, "Combined contract must inherit " + name + ".");
                    foreach (var name in new[] { "DbReader", "DbWriter" })
                        if (!combined.Members.OfType<PropertyDeclarationSyntax>().Any(property => property.Identifier.ValueText == name))
                            Add(source, combined, "Combined contract must expose " + name + ".");
                }
        }
        if (rule == "ContractMethods")
        {
            var assembly = typeof(DbContextFactory).Assembly;
            var implementation = assembly.GetTypes().Single(type => type.Name == spec.Context);
            var contracts = assembly.GetTypes().Where(type => type.IsInterface && (type.Name == spec.Read || type.Name == spec.Write))
                .SelectMany(type => type.GetInterfaces().Append(type)).Distinct().ToArray();
            var targets = contracts.SelectMany(contract => implementation.GetInterfaceMap(contract).TargetMethods).ToArray();
            foreach (var method in implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && !(method.IsStatic && method.Name.StartsWith("MapTo", StringComparison.Ordinal))))
                if (!targets.Any(target => target.Module == method.Module && target.MetadataToken == method.MetadataToken))
                {
                    var owner = context.First();
                    var declaration = owner.Type.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(node => node.Identifier.ValueText == method.Name);
                    Add(owner.Source, declaration ?? (SyntaxNode)owner.Type, "Public method is not declared on the read/write contracts (including inherited contracts): " + method.Name);
                }
        }
        foreach (var source in sources)
        {
            var inSchema = source.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Schema", StringComparer.Ordinal);
            var file = Path.GetFileName(source.Path);
            if (rule == "Helpers" && spec.Name != "SchemaDb" && file != spec.Extensions)
                foreach (var member in source.Root.DescendantNodes().OfType<MemberDeclarationSyntax>()
                    .Where(node => node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax
                        && node is EnumDeclarationSyntax or DelegateDeclarationSyntax))
                    Add(source, member, "Persistence support declarations belong in " + spec.Extensions + ".");
            foreach (var type in source.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var name = type.Identifier.ValueText;
                if (rule == "Helpers" && spec.Name != "SchemaDb" && type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax
                    && !IsOwnedDeclaration(spec, type, file))
                    Add(source, type, "Declaration belongs in its context, extensions, parameter, catalog or contract file: " + name);
                if (rule == "Helpers" && name == spec.Context)
                    foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
                        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword) && method.ExplicitInterfaceSpecifier is null
                            && !(method.Modifiers.Any(SyntaxKind.StaticKeyword) && method.Identifier.ValueText.StartsWith("MapTo", StringComparison.Ordinal)))
                            Add(source, method, "Non-contract helper must live in the context extension class: " + method.Identifier.ValueText);
                if (rule == "Helpers" && name.EndsWith("ContextExtensions", StringComparison.Ordinal))
                {
                    if (name != spec.Context + "Extensions") Add(source, type, "Unexpected context extension class.");
                    foreach (var method in type.DescendantNodes().OfType<MethodDeclarationSyntax>())
                    {
                        if (!method.Ancestors().Any(IsExtensionBlock) || method.ParameterList.Parameters.Any(parameter => parameter.Modifiers.Any(SyntaxKind.ThisKeyword)))
                            Add(source, method, "Helpers must use C# 14 receiver blocks, not ordinary static/legacy extension methods.");
                        if (spec.Name == "ConfigurationDb" && !HasDocumentation(method))
                            Add(source, method, "Configuration persistence helpers must have XML documentation.");
                    }
                    var duplicates = type.DescendantNodes().Where(IsExtensionBlock).Select(block => block.ChildNodes().OfType<ParameterListSyntax>().FirstOrDefault())
                        .Where(parameters => parameters is not null).GroupBy(parameters => Compact(parameters!.ToString()), StringComparer.Ordinal).Where(group => group.Count() > 1);
                    foreach (var duplicate in duplicates) Add(source, type, "Repeated receiver/role block: " + duplicate.Key);
                }
                if (rule == "Parameters" && type.BaseList?.Types.Any(item => item.Type.ToString().EndsWith("IBindValue", StringComparison.Ordinal)) == true)
                {
                    if (file != spec.Parameters) Add(source, type, "Binding must be consolidated in " + spec.Parameters + ".");
                    if (type is not RecordDeclarationSyntax record || !record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                        || !type.Modifiers.Any(SyntaxKind.InternalKeyword) || !type.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                        Add(source, type, "Bindings must be internal readonly record structs.");
                }
                if (rule == "Ownership" && !inSchema && !file.EndsWith("Sql.cs", StringComparison.Ordinal))
                    foreach (var method in type.Members.OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.ValueText.StartsWith("Rebuild", StringComparison.Ordinal)))
                        Add(source, method, "Rebuild/cache reconstruction sequencing belongs to the domain/application owner.");
                if (rule == "Ownership" && name is "ConfigurationProjectionBaseline" or "InstrumentDefinitionCatalogPublisher" or "TradePlanChartCache")
                    Add(source, type, "Known application orchestration/cache owner remains in Storage: " + name);
            }
            if (rule == "Ownership")
                foreach (var field in source.Root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                    .Where(field => field.Declaration.Type.DescendantNodesAndSelf().OfType<GenericNameSyntax>()
                        .Any(name => name.Identifier.ValueText == "AsyncReadCache")))
                    Add(source, field, "Application-owned version/expiry cache policy must be outside Storage.");
            foreach (var invocation in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var name = InvocationName(invocation);
                if (rule == "Validation" && IsRequestValidation(invocation)) Add(source, invocation, "Incoming validation belongs to the caller: " + invocation.Expression);
                if (rule == "Validation" && spec.Name == "ConfigurationDb" && name == "ValidateParameters")
                    Add(source, invocation, "Draft parameter shape validation belongs to the caller; storage verifies the schema proof.");
                if (rule == "Ownership" && spec.Name != "SequenceIdDb" && (name is "GetSequenceIdAsync" or "GetSequenceIdsAsync"))
                    Add(source, invocation, "Allocate application identities in the owning producer before persistence: " + invocation.Expression);
                if (rule == "Ownership" && name is "ValidateProductAsync" or "ValidateLegacyFamilyAsync" or "ValidateCatalogGraph" or "ValidateCapability" or "GetCompletedMarketCloseDate")
                    Add(source, invocation, "Domain/reference/schedule decision remains in persistence: " + invocation.Expression);
                if (rule == "Ownership" && spec.Name == "PortfolioDb" && invocation.Expression.ToString().Contains("PortfolioProjection<FundMandateReadModel>.Create", StringComparison.Ordinal))
                    Add(source, invocation, "The domain must supply Fund version/state projections; persistence must not choose disabled versions.");
                if (rule == "Ownership" && name == "GetHistoricalDataLoadAsync" && invocation.Ancestors().OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "GetHistoricalAnalyticsAttemptProjectionAsync"))
                    Add(source, invocation, "Diagnostics query must use projected read models, not reconstruct progress from command/import state.");
                if (rule == "Ownership" && invocation.Expression.ToString().Contains("CompositionPreparationService.Validate", StringComparison.Ordinal))
                    Add(source, invocation, "Composition validation belongs to the owning command producer.");
                if (rule == "Ownership" && spec.Name == "EventSourceDb"
                    && invocation.Expression.ToString().EndsWith("RiskCompletionProjection.WriteAsync", StringComparison.Ordinal))
                    Add(source, invocation, "Risk completion replay and Trade projection coordination belong to Trade.");
            }
            if (rule == "Validation")
                foreach (var statement in source.Root.DescendantNodes().OfType<ThrowStatementSyntax>())
                    if (statement.Expression is ObjectCreationExpressionSyntax creation && creation.Type.ToString().StartsWith("Argument", StringComparison.Ordinal)
                        && statement.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } method
                        && !method.Identifier.ValueText.StartsWith("MapTo", StringComparison.Ordinal)
                        && !IsStorageCursorScopeFence(statement)
                        && !IsPostgresOptionCursorFence(method, statement)
                        && !IsTradeKeysetCursorFence(method, statement, creation)
                        && !IsMarketDataCursorFence(method, statement, creation)
                        && method.Ancestors().OfType<TypeDeclarationSyntax>().Any(type => type.Identifier.ValueText == spec.Context || type.Identifier.ValueText == spec.Context + "Extensions"))
                        Add(source, statement, "Incoming argument rejection must be performed by the caller, preserving database integrity guards.");
            if (rule == "Ownership")
                foreach (var creation in source.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                    if (creation.Type.ToString().EndsWith("Event", StringComparison.Ordinal)) Add(source, creation, "Storage must persist supplied events, not construct domain events: " + creation.Type);
                    else if (spec.Name == "PortfolioDb" && creation.Type.ToString().EndsWith("PortfolioProjection", StringComparison.Ordinal))
                        Add(source, creation, "Choose the additional Fund projection in its domain owner, not during persistence.");
            if (rule is "Statements" or "Dialect" or "Schema")
                foreach (var (node, text) in Strings(source))
                {
                    if (!LooksLikeSql(text) && !LooksLikeSqlFragment(text)) continue;
                    if (rule == "Dialect" && spec.Name != "TradeTickQuoteDb"
                        && Regex.IsMatch(text, @"\btoken\s*\(|\bALLOW\s+FILTERING\b|\bCREATE\s+KEYSPACE\b|\bCLUSTERING\s+ORDER\s+BY\b", RegexOptions.IgnoreCase))
                        Add(source, node, "ScyllaDB CQL remains in a PostgreSQL context.");
                    if (rule == "Dialect" && spec.Name == "TradeTickQuoteDb"
                        && Regex.IsMatch(text, @"\$\d+|::\w|\bON\s+CONFLICT\b|\bRETURNING\b|\bCREATE\s+SCHEMA\b", RegexOptions.IgnoreCase))
                        Add(source, node, "PostgreSQL SQL belongs outside the ScyllaDB raw tick context.");
                    if (rule == "Statements" && file != spec.RuntimeCatalog && file != spec.SchemaCatalog)
                        Add(source, node, "Statement is outside the owned runtime/schema catalog.");
                    if (rule == "Schema" && Regex.IsMatch(text, @"^\s*(CREATE|ALTER|DROP)\b", RegexOptions.IgnoreCase)
                        && file != spec.SchemaCatalog)
                        Add(source, node, "DDL/initialization definitions belong in the schema catalog.");
                    if (rule == "Schema" && Regex.IsMatch(text, @"\b(?:TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?|FROM\s+|JOIN\s+|INTO\s+)(?:\w+\.)?\w+_v\d+\b", RegexOptions.IgnoreCase))
                        Add(source, node, "Physical table name has a prohibited schema-generation suffix.");
                }
            if (rule == "Statements" && file != spec.RuntimeCatalog && file != spec.SchemaCatalog)
                foreach (var addition in source.Root.DescendantNodes().OfType<BinaryExpressionSyntax>()
                    .Where(expression => expression.IsKind(SyntaxKind.AddExpression)))
                    if (addition.Left is MemberAccessExpressionSyntax catalogMember
                        && catalogMember.Expression.ToString() == spec.Stem + "DbSql")
                        Add(source, addition, "Select a complete statement or fill a runtime SQL template in the extensions instead of appending SQL to a catalog statement.");
            if (rule == "Dialect" && spec.Name != "TradeTickQuoteDb" && (file.Contains("Cql", StringComparison.Ordinal)
                || source.Root.DescendantTokens().Any(token => token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText.EndsWith("DbCql", StringComparison.Ordinal))))
                Add(source, source.Root, "Retire the non-tick CQL catalog/fallback.");
            if (rule == "Parameters")
                foreach (var creation in source.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                    if (creation.Type.ToString().EndsWith("NpgsqlParameter", StringComparison.Ordinal) || creation.Type.ToString().EndsWith("SqlParameter", StringComparison.Ordinal))
                        Add(source, creation, "Context bindings must use provider-neutral values.");
            if (rule == "Schema" && inSchema && file.EndsWith("Sql.cs", StringComparison.Ordinal) && file != spec.SchemaCatalog)
                Add(source, source.Root, "Multiple/feature-specific schema SQL catalogs are prohibited.");
            if (rule == "Mappers")
            {
                foreach (var local in source.Root.DescendantNodes().OfType<LocalFunctionStatementSyntax>()
                    .Where(method => method.ParameterList.Parameters.Any(parameter =>
                        parameter.Type?.ToString().EndsWith("IObjectDataRecord", StringComparison.Ordinal) == true
                        || parameter.Type?.ToString().EndsWith("DbDataReader", StringComparison.Ordinal) == true)))
                    Add(source, local, "Record mapper must be a static MapTo method declared on the context.");

                foreach (var call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(call => InvocationName(call) is "ExecuteSingleAsync" or "ExecuteQueryAsync" or "ExecuteScalarAsync" or "ExecutePageAsync" or "ExecuteStreamAsync" or "ExecuteMapReduceAsync" or "QueryAsync"))
                    foreach (var argument in call.ArgumentList.Arguments.Where(argument => argument.Expression is AnonymousFunctionExpressionSyntax))
                    {
                        // Map/reduce takes a mapper first and a reducer second. A reducer is not a record mapper.
                        if (InvocationName(call) == "ExecuteMapReduceAsync" && call.ArgumentList.Arguments.IndexOf(argument) != 0)
                            continue;
                        // A forwarding closure supplies query-scoped facts to an explicit static MapTo method.
                        // It must not read record columns or construct a result inline.
                        if (argument.Expression is LambdaExpressionSyntax { Body: InvocationExpressionSyntax forwarded }
                            && InvocationName(forwarded).StartsWith("MapTo", StringComparison.Ordinal)
                            && !forwarded.ArgumentList.DescendantNodes().OfType<InvocationExpressionSyntax>().Any())
                            continue;
                        Add(source, argument, "Inline result mapper must be a static MapTo method with explicit ordinals.");
                    }
                foreach (var method in source.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                    if (method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().EndsWith("IObjectDataRecord", StringComparison.Ordinal) == true)
                        && (!method.Identifier.ValueText.StartsWith("MapTo", StringComparison.Ordinal) || !method.Modifiers.Any(SyntaxKind.StaticKeyword)))
                        Add(source, method, "Record mapper must be static MapTo... .");
            }
            if (rule == "Documentation")
                foreach (var type in source.Root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(type => type.Identifier.ValueText == spec.Context
                    || type.Identifier.ValueText == spec.Read || type.Identifier.ValueText == spec.Write || type.Identifier.ValueText == spec.Combined))
                {
                    if (!HasDocumentation(type)) Add(source, type, "Missing public type/constructor XML summary.");
                    if (type.ParameterList is { } primaryConstructor)
                        foreach (var parameter in primaryConstructor.Parameters)
                            if (!HasParameterDocumentation(type, parameter.Identifier.ValueText))
                                Add(source, parameter, "Missing/empty primary-constructor parameter documentation: " + parameter.Identifier.ValueText);
                    foreach (var member in type.Members.Where(member => type is InterfaceDeclarationSyntax || MemberModifiers(member).Any(SyntaxKind.PublicKeyword)))
                        if (member is MethodDeclarationSyntax or PropertyDeclarationSyntax or ConstructorDeclarationSyntax)
                        {
                            if (!HasDocumentation(member)) Add(source, member, "Missing public member XML documentation.");
                            if (member is ConstructorDeclarationSyntax constructor && !Documentation(member).Contains("inheritdoc", StringComparison.Ordinal))
                                foreach (var parameter in constructor.ParameterList.Parameters)
                                    if (!HasParameterDocumentation(member, parameter.Identifier.ValueText))
                                        Add(source, parameter, "Missing/empty constructor parameter documentation: " + parameter.Identifier.ValueText);
                            if (member is MethodDeclarationSyntax method && !Documentation(member).Contains("inheritdoc", StringComparison.Ordinal))
                            {
                                foreach (var parameter in method.ParameterList.Parameters)
                                    if (!HasParameterDocumentation(member, parameter.Identifier.ValueText))
                                        Add(source, parameter, "Missing/empty parameter documentation: " + parameter.Identifier.ValueText);
                                if (method.ReturnType.ToString() != "void" && !Regex.IsMatch(Documentation(member), @"<returns>\s*(?:[^<\s]|<see\b|<paramref\b)"))
                                    Add(source, method, "Missing/empty return documentation.");
                            }
                        }
                }
            if (rule == "FluentCalls")
            {
                var calls = source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call => IsPersistenceCall(InvocationName(call))).ToArray();
                foreach (var call in calls)
                {
                    var arguments = call.ArgumentList.GetLocation().GetLineSpan();
                    if (arguments.StartLinePosition.Line != arguments.EndLinePosition.Line) Add(source, call.ArgumentList, "Extract multiline arguments before the persistence chain: " + InvocationName(call));
                }
                foreach (var group in calls.GroupBy(call => (call.Expression is MemberAccessExpressionSyntax member ? member.Name : call.Expression).GetLocation().GetLineSpan().StartLinePosition.Line).Where(group => group.Count() > 1))
                    Add(source, group.First(), "Each fluent persistence invocation must occupy its own line.");
            }
        }
        if (rule == "QueryResults") InspectQueryResults(spec, sources, errors);
        if (rule == "Documentation") InspectInheritedDocumentation(spec, errors);
        return errors;
    }

    static readonly Lazy<Dictionary<string, string[]>> ContractSourcePaths = new(() =>
    {
        var files = new List<string>();
        var pending = new Stack<string>(Directory.GetDirectories(DbContextConventionSource.RepositoryRoot, "TomasAI.IFM.*"));
        while (pending.TryPop(out var folder))
        {
            files.AddRange(Directory.GetFiles(folder, "I*.cs"));
            foreach (var child in Directory.GetDirectories(folder))
                if (Path.GetFileName(child) is not ("bin" or "obj" or "TestResults" or "target" or "node_modules" or "History" or "Logs")
                    && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    pending.Push(child);
        }
        return files.GroupBy(Path.GetFileName, StringComparer.Ordinal).ToDictionary(group => group.Key!, group => group.ToArray(), StringComparer.Ordinal);
    });

    static void InspectInheritedDocumentation(StorageContextSpec spec, List<string> errors)
    {
        var contracts = typeof(DbContextFactory).Assembly.GetTypes()
            .Where(type => type.IsInterface && (type.Name == spec.Read || type.Name == spec.Write))
            .SelectMany(type => type.GetInterfaces()).Distinct();
        foreach (var contract in contracts.Where(type => type.Assembly.GetName().Name?.StartsWith("TomasAI.IFM.", StringComparison.Ordinal) == true))
        {
            var contractName = contract.Name.Split('`')[0];
            var paths = ContractSourcePaths.Value.GetValueOrDefault(contractName + ".cs", []);
            var localSources = Sources.Value[spec.Name].Where(source =>
                source.Root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Any(type => type.Identifier.ValueText == contractName));
            var matchingSources = localSources.Concat(paths.Select(path => StorageSource.Parse(path, File.ReadAllText(path))))
                .DistinctBy(source => source.Path);
            var found = false;
            foreach (var source in matchingSources)
            {
                foreach (var declaration in source.Root.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                    .Where(type => type.Identifier.ValueText == contractName
                        && type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() == contract.Namespace))
                {
                    found = true;
                    foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        var location = $"{spec.Name}/Documentation: {source.Path}:{method.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                        if (!HasDocumentation(method)) errors.Add(location + ": Missing inherited contract member documentation.");
                        if (Documentation(method).Contains("inheritdoc", StringComparison.Ordinal)) continue;
                        foreach (var parameter in method.ParameterList.Parameters)
                            if (!HasParameterDocumentation(method, parameter.Identifier.ValueText))
                                errors.Add(location + ": Missing inherited parameter documentation: " + parameter.Identifier.ValueText);
                        if (method.ReturnType.ToString() != "void" && !Regex.IsMatch(Documentation(method), @"<returns>\s*(?:[^<\s]|<see\b|<paramref\b)"))
                            errors.Add(location + ": Missing inherited return documentation.");
                    }
                }
            }
            if (!found) errors.Add($"{spec.Name}/Documentation: No matching source declaration for inherited contract {contract.FullName}.");
        }
    }

    static void InspectQueryResults(StorageContextSpec spec, StorageSource[] sources, List<string> errors)
    {
        var assembly = typeof(DbContextFactory).Assembly;
        var contract = assembly.GetTypes().SingleOrDefault(type => type.Name == spec.Read);
        if (contract is null) { errors.Add($"{spec.Name}/QueryResults: Read contract is missing."); return; }
        foreach (var method in contract.GetInterfaces().Append(contract).SelectMany(type => type.GetMethods()).Distinct())
        {
            var seen = new HashSet<Type>();
            void Visit(Type type)
            {
                if (type.IsGenericParameter || !seen.Add(type)) return;
                if (type.IsArray) { Visit(type.GetElementType()!); return; }
                if (type.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true)
                    errors.Add($"{spec.Name}/QueryResults: {contract.Name}.{method.Name} returns a public query tuple.");
                if (type.Assembly == assembly && !type.IsEnum && !type.IsInterface && !type.IsPrimitive)
                    errors.Add($"{spec.Name}/QueryResults: {contract.Name}.{method.Name} exposes Storage-owned result {type.FullName}.");
                foreach (var argument in type.GenericTypeArguments) Visit(argument);
            }
            Visit(method.ReturnType);
        }
    }

    // Scope fingerprints prevent the physical keyset from being reused with a different query or key layout.
    static bool IsStorageCursorScopeFence(ThrowStatementSyntax statement)
    {
        var condition = statement.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault()?.Condition;
        if (condition is null || !statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(call => InvocationName(call) == "nameof" && call.ArgumentList.Arguments.Any(a => a.Expression.ToString() == "cursor"))) return false;
        var guard = Compact(condition.ToString());
        return guard.Contains("string.Equals(resume.Scope,scopeId,StringComparison.Ordinal)", StringComparison.Ordinal)
            && guard.Contains("resume.Keys.Length!=order.Length", StringComparison.Ordinal);
    }

    static bool IsPostgresOptionCursorFence(MethodDeclarationSyntax method, ThrowStatementSyntax statement)
    {
        if (method.Identifier.ValueText != "ReadNativeOptionPageAsync" ||
            !method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "QualifiedOptionCatalogPage"))
            return false;
        var condition = Compact(statement.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault()?.Condition.ToString() ?? string.Empty);
        if (condition == "cursorisnull||!string.Equals(cursor.Symbol,request.Symbol,StringComparison.Ordinal)||cursor.PageSize!=request.PageSize")
            return true;
        var handler = statement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
        return handler is not null && Compact(handler.Filter?.FilterExpression.ToString() ?? string.Empty)
            == "errorisFormatExceptionorSystem.Text.Json.JsonException" && handler.Parent?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>().Any(call => InvocationName(call) == "FromBase64String"
                    && call.ArgumentList.Arguments.Any(argument => argument.Expression.ToString() == "request.ContinuationToken")) == true;
    }

    static bool IsTradeKeysetCursorFence(MethodDeclarationSyntax method, ThrowStatementSyntax statement,
        ObjectCreationExpressionSyntax exception)
    {
        if (!method.Ancestors().OfType<ClassDeclarationSyntax>()
            .Any(type => type.Identifier.ValueText == "TradeDbContextExtensions"))
            return false;

        var cursorParameter = method.Identifier.ValueText switch
        {
            "TradeReadEstablishedHistoryAsync" or "TradeReadPositionHistoryAsync" or "TradeReadRoutePageAsync" => "cursor",
            "PipelineReadReadHistoryAsync" or "RiskReadHistoryAsync" => "pagingState",
            _ => null
        };
        if (cursorParameter is null || exception.ArgumentList?.Arguments.Count < 2 ||
            exception.ArgumentList?.Arguments[1].Expression.ToString() != $"nameof({cursorParameter})")
            return false;

        return statement.Ancestors().OfType<IfStatementSyntax>().Any(branch =>
            Compact(branch.Condition.ToString()) == cursorParameter + "isnotnull" &&
            branch.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                InvocationName(call) == "Deserialize" &&
                call.Expression.ToString().StartsWith("JsonSerializer.Deserialize<", StringComparison.Ordinal) &&
                call.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() == cursorParameter));
    }

    static bool IsMarketDataCursorFence(MethodDeclarationSyntax method, ThrowStatementSyntax statement,
        ObjectCreationExpressionSyntax exception)
    {
        if (!method.Ancestors().OfType<ClassDeclarationSyntax>()
            .Any(type => type.Identifier.ValueText == "MarketDataDbContextExtensions"))
            return false;

        if (method.Identifier.ValueText == "DecodePageToken"
            && exception.ArgumentList?.Arguments.ElementAtOrDefault(1)?.Expression.ToString() == "nameof(token)")
        {
            var handler = statement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
            return handler is not null
                && Compact(handler.Filter?.FilterExpression.ToString() ?? "") == "exisFormatExceptionorJsonException"
                && handler.Parent?.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => call.Expression.ToString() == "JsonSerializer.Deserialize<CalendarPageToken>") == true;
        }

        return method.Identifier.ValueText == "ReadOptionIvHistoryAsync"
            && exception.ArgumentList?.Arguments.ElementAtOrDefault(1)?.Expression.ToString() == "nameof(request)"
            && statement.Ancestors().OfType<IfStatementSyntax>().Any(branch =>
                Compact(branch.Condition.ToString()) == "request.PagingStateisnotnull"
                && branch.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                    call.Expression.ToString() == "JsonSerializer.Deserialize<VolatilityHistoryCursor>"
                    && call.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString() == "request.PagingState"));
    }

    static bool IsRequestValidation(InvocationExpressionSyntax call)
    {
        if (call.Ancestors().OfType<ConstructorDeclarationSyntax>().Any()) return false;
        var name = InvocationName(call);
        if (name is "ValidateRequiredArgument" or "ValidateTextArgument" or "ValidateNonnegativeArgument"
            or "ValidatePositiveArgument" or "ValidateBatchSize" or "ValidateProjectorIdentity"
            or "ValidateExecutionToken" or "ValidateOutboxReleaseStatus" or "ValidateConventionBatch"
            or "ValidateAndNormalizeSymbol" or "ValidateAndNormalizeExpiryRange" or "ValidateAndNormalizeCachedDefinitionQuery"
            or "RequirePageSize" or "RequireNotBefore" or "ValidateRoutePageSize"
            or "ValidateSnapshot" or "ValidateForPublication" or "ValidateCatalogGraph" or "ValidatePagingRequest"
            or "ValidateMaximumDefinitionExpiries") return true;
        var expression = call.Expression.ToString();
        if (!expression.Contains("Argument", StringComparison.Ordinal) || !name.StartsWith("Throw", StringComparison.Ordinal)) return false;
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method?.Identifier.ValueText.StartsWith("MapTo", StringComparison.Ordinal) == true) return false;
        // An enlisted transaction is a persistence resource regardless of its parameter name.
        // Require its declared type so a business request named "enlisted" is still flagged.
        var checkedValue = call.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString();
        // The executor rejects missing SQL binding data or a missing row mapper before touching the provider.
        if (method?.Parent is ClassDeclarationSyntax { Identifier.ValueText: "EnlistedEventTransaction" }
            && (method.Identifier.ValueText, checkedValue) is
                ("ExecuteAsync" or "QueryAsync" or "ScalarAsync", "parameters") or
                ("ExecuteBatchAsync", "statements") or ("QueryAsync", "map"))
            return false;
        if (method?.Parent is ClassDeclarationSyntax { Identifier.ValueText: "PostgresEventTransaction" }
            && (method.Identifier.ValueText, checkedValue) is
                ("ResolveConnectionString", "settings") or ("ExecuteOnceAsync" or "ExecuteAsync", "operation"))
            return false;
        if (method?.Parent is RecordDeclarationSyntax { Identifier.ValueText: "PostgresDatabaseIdentity" }
            && (method.Identifier.ValueText, checkedValue) is
                ("Parse", "connectionString") or ("RequireSamePhysicalDatabase", "settings"))
            return false;
        if (method is not null && IsEnlistedTransactionResource(method, checkedValue))
            return false;
        // Connection/transaction/record integrity remains a persistence responsibility.
        return checkedValue is not ("connection" or "transaction" or "reader" or "row" or "record" or "buffer");
    }

    static bool IsEnlistedTransactionResource(MethodDeclarationSyntax method, string? name)
    {
        static bool Matches(ParameterSyntax parameter, string? expectedName) =>
            parameter.Identifier.ValueText == expectedName && (parameter.Type switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText is
                    "EnlistedEventTransaction" or "IEnlistedPostgresTransaction",
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText is
                    "EnlistedEventTransaction" or "IEnlistedPostgresTransaction",
                _ => false
            });

        if (method.ParameterList.Parameters.Any(parameter => Matches(parameter, name)))
            return true;
        return method.Ancestors().Where(IsExtensionBlock)
            .SelectMany(block => block.ChildNodes().OfType<ParameterListSyntax>())
            .Any(list => list.Parameters.Any(parameter => Matches(parameter, name)));
    }

    static bool IsExtensionBlock(SyntaxNode node) => node.Kind().ToString().StartsWith("Extension", StringComparison.Ordinal)
        && node.ChildNodes().OfType<ParameterListSyntax>().Any();
    static string Compact(string text) => Regex.Replace(text, @"\s+", "");
    static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => string.Empty
    };
    static bool IsPersistenceCall(string name) => name is "Use" or "SetParameters" or "ExecuteCommandAsync" or "ExecuteSingleAsync" or "ExecuteQueryAsync"
        or "ExecuteScalarAsync" or "ExecutePageAsync" or "QueueCommand" or "ConfigureAwait";
    static bool IsOwnedDeclaration(StorageContextSpec spec, TypeDeclarationSyntax type, string file)
    {
        var name = type.Identifier.ValueText;
        if (name == spec.Context) return file == spec.Context + ".cs";
        if (name == spec.Context + "Extensions") return file == spec.Extensions;
        if (name == spec.Combined || name == spec.Read || name == spec.Write) return file == name + ".cs";
        if (type is InterfaceDeclarationSyntax)
            return file == spec.Combined + ".cs" || file == spec.Read + ".cs" || file == spec.Write + ".cs";
        if (type.BaseList?.Types.Any(item => item.Type.ToString().EndsWith("IBindValue", StringComparison.Ordinal)
            || item.Type.ToString().EndsWith("IScyllaOwnedBindValues", StringComparison.Ordinal)) == true)
            return file == spec.Parameters;
        // Positional values can have their own type before being collected into an IBindValue.
        if (file == spec.Parameters)
            return type is RecordDeclarationSyntax { ParameterList: not null } record
                && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                && record.Modifiers.Any(SyntaxKind.InternalKeyword) && record.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        if (file == spec.RuntimeCatalog)
            return type is ClassDeclarationSyntax && type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax
                && name == spec.Stem + "DbSql";
        if (file == spec.SchemaExecutor)
            return type.BaseList?.Types.Any(item => Compact(item.Type.ToString()).StartsWith("SchemaDbContext<", StringComparison.Ordinal)) == true;
        return file == spec.Extensions || file == spec.SchemaCatalog;
    }

    static bool LooksLikeSql(string text) => Regex.IsMatch(text, @"^\s*(SELECT\b|INSERT\s+INTO\b|UPDATE\s+\w|DELETE\s+FROM\b|WITH\s+\w|CREATE\b|ALTER\b|DROP\b|TRUNCATE\b|BEGIN\s+.*BATCH\b)", RegexOptions.IgnoreCase);
    static bool LooksLikeSqlFragment(string text) => Regex.IsMatch(text,
        @"^\s*(WHERE\s+\S[\s\S]*(\$\d|\?|\bORDER\s+BY\b)|JOIN\s+\S+\s+\S*\s*ON\s+\S)", RegexOptions.IgnoreCase);
    static IEnumerable<(SyntaxNode Node, string Text)> Strings(StorageSource source)
    {
        foreach (var literal in source.Root.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)))
            yield return (literal, literal.Token.ValueText);
        foreach (var text in source.Root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
            yield return (text, string.Concat(text.Contents.OfType<InterpolatedStringTextSyntax>().Select(part => part.TextToken.ValueText)));
    }
    static string Documentation(SyntaxNode node) => string.Concat(node.GetLeadingTrivia()
        .Where(trivia => trivia.GetStructure() is DocumentationCommentTriviaSyntax).Select(trivia => trivia.ToFullString()));
    static bool HasDocumentation(SyntaxNode node) => Regex.IsMatch(Documentation(node), @"<(summary|inheritdoc)\b");
    static bool HasParameterDocumentation(SyntaxNode node, string parameter)
        => Regex.IsMatch(Documentation(node), "<param\\s+name\\s*=\\s*\"" + Regex.Escape(parameter) + "\"[^>]*>\\s*[^<\\s]");
    static SyntaxTokenList MemberModifiers(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Modifiers,
        PropertyDeclarationSyntax property => property.Modifiers,
        ConstructorDeclarationSyntax constructor => constructor.Modifiers,
        _ => default
    };
}

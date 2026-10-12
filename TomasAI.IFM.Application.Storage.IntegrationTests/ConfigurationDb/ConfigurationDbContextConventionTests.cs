using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

public sealed class ConfigurationDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<ConfigurationDbContext>)
            .IsAssignableFrom(typeof(IConfigurationDbContext)));
        Assert.True(typeof(IConfigurationDbReadContext)
            .IsAssignableFrom(typeof(IConfigurationDbContext)));
        Assert.True(typeof(IConfigurationDbWriteContext)
            .IsAssignableFrom(typeof(IConfigurationDbContext)));
        Assert.True(typeof(IConfigurationDbContext)
            .IsAssignableFrom(typeof(ConfigurationDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_configuration_context()
    {
        var property = typeof(IDbContextFactory)
            .GetProperty(nameof(IDbContextFactory.ConfigurationDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IConfigurationDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_contract_implementations()
    {
        Type[] contracts =
        [
            typeof(IConfigurationDbReadContext),
            typeof(IConfigurationDbWriteContext)
        ];
        var contractMethods = contracts
            .SelectMany(static type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(ConfigurationDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .ToArray();

        Assert.All(
            publicMethods,
            implementation => Assert.Contains(
                contractMethods,
                contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(ConfigurationDbContext)
            .GetMethods(
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.DeclaredOnly)
            .Where(static method =>
                !method.IsSpecialName
                && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(
            declared.Where(static method => method.IsStatic),
            method => Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(
            declared,
            static method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(ConfigurationDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.ConfigurationDb.ConfigurationDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions
            .GetMethods(
                BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly)
            .Where(static method =>
                !method.IsSpecialName
                && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();
        Assert.NotEmpty(extensionMethods);
        Assert.All(
            extensionMethods,
            method => Assert.True(
                method.IsDefined(typeof(ExtensionAttribute), false),
                $"{method.Name} must be an extension method."));
    }

    [Fact]
    public void Folder_has_no_partial_context_implementations_or_context_argument_validation()
    {
        var folder = GetFolder();
        var source = Directory
            .GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadAllLines);
        var context = File.ReadAllText(
            Path.Combine(folder, "ConfigurationDbContext.cs"));
        var extensions = File.ReadAllText(
            Path.Combine(folder, "ConfigurationDbContextExtensions.cs"));

        Assert.DoesNotContain(
            source,
            line => line.Contains(
                "partial class ConfigurationDbContext",
                StringComparison.Ordinal));
        Assert.DoesNotContain("throw new Argument", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.Contains(
            "extension(ConfigurationDbContext context)",
            extensions,
            StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        string[] boundaries =
        [
            ").SetParameters(",
            ").ExecuteCommandAsync(",
            ").ExecuteSingleAsync(",
            ").ExecuteQueryAsync(",
            ").ExecuteScalarAsync(",
            ").ExecutePageAsync(",
            ").QueueCommand(",
            ").ConfigureAwait("
        ];

        foreach (var file in Directory.GetFiles(
                     GetFolder(),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(
                File.ReadAllLines(file),
                line => boundaries.Any(
                    boundary => line.Contains(boundary, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void Parameter_bindings_are_internal_and_owned_by_one_catalog()
    {
        var folder = GetFolder();
        var catalog = Path.Combine(folder, "ConfigurationDbParameters.cs");
        Assert.True(File.Exists(catalog));
        foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly))
        {
            if (file.EndsWith("ConfigurationDbContext.cs", StringComparison.Ordinal)
                || file.EndsWith("ConfigurationDbContextExtensions.cs", StringComparison.Ordinal))
                continue;
            if (File.ReadAllText(file).Contains(": IBindValue", StringComparison.Ordinal))
                Assert.Equal(catalog, file);
        }
        Assert.DoesNotContain("public readonly record struct", File.ReadAllText(catalog), StringComparison.Ordinal);
    }

    [Fact]
    public void Public_storage_methods_document_parameters_and_results_in_the_context()
    {
        var source = File.ReadAllText(Path.Combine(GetFolder(), "ConfigurationDbContext.cs"));
        var signatures = System.Text.RegularExpressions.Regex.Matches(source,
            @"public\s+(?:async\s+)?[^\n(]+?\b(?<name>\w+Async)\((?<parameters>[^)]*)\)");
        var methods = typeof(ConfigurationDbContext).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName).ToArray();
        Assert.Equal(methods.Length, signatures.Count);
        foreach (System.Text.RegularExpressions.Match signature in signatures)
        {
            var start = source.LastIndexOf("/// <summary>", signature.Index, StringComparison.Ordinal);
            Assert.True(start >= 0);
            var documentation = source[start..signature.Index];
            Assert.Contains("<returns>", documentation, StringComparison.Ordinal);
            var method = methods.Single(candidate => candidate.Name == signature.Groups["name"].Value
                && candidate.GetParameters().Length == signature.Groups["parameters"].Value.Split(',').Length
                && candidate.GetParameters().All(parameter =>
                    System.Text.RegularExpressions.Regex.IsMatch(signature.Groups["parameters"].Value,
                        @"\b" + parameter.Name + @"\b")));
            foreach (var parameter in method.GetParameters())
                Assert.Matches("<param name\\s*=\\s*\"" + parameter.Name + "\">", documentation);
        }
    }

    [Fact]
    public void PostgreSql_schema_uses_one_complete_sql_catalog_and_schema_executor()
    {
        var folder = GetFolder();
        var schema = Path.Combine(folder, "Schema");
        Assert.Equal(new[] { "ConfigurationSchemaDb.cs", "ConfigurationSchemaSql.cs" },
            Directory.GetFiles(schema, "*.cs").Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(folder, "ParameterSets")));
        Assert.Empty(Directory.GetFiles(folder, "*.cql", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(folder, "*Cql.cs", SearchOption.AllDirectories));
        var definitions = TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaSql.GetDefinitions();
        Assert.Equal(definitions.Length, definitions.Select(definition => definition.Name).Distinct().Count());
        Assert.Equal("reference_configuration", definitions[0].Name);
        var sql = string.Join("\n", definitions.Select(definition => definition.CreateStatement));
        Assert.Contains("CREATE TABLE", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE OR REPLACE FUNCTION", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TRIGGER", sql, StringComparison.Ordinal);
        Assert.Contains("configuration_baseline_application", sql, StringComparison.Ordinal);
    }

    private static bool SameSignature(
        MethodInfo contract,
        MethodInfo implementation) =>
        contract.Name == implementation.Name
        && contract.ReturnType == implementation.ReturnType
        && contract
            .GetParameters()
            .Select(static parameter => parameter.ParameterType)
            .SequenceEqual(
                implementation
                    .GetParameters()
                    .Select(static parameter => parameter.ParameterType));

    private static string GetFolder() =>
        Path.Combine(
            FindRepositoryRoot(),
            "TomasAI.IFM.Application.Storage",
            "ConfigurationDb");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root.");
    }
}

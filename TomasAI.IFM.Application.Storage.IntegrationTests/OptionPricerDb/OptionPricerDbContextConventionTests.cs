using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.OptionPricerDb;

public sealed class OptionPricerDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<OptionPricerDbContext>).IsAssignableFrom(typeof(IOptionPricerDbContext)));
        Assert.True(typeof(IOptionPricerDbReadContext).IsAssignableFrom(typeof(IOptionPricerDbContext)));
        Assert.True(typeof(IOptionPricerDbWriteContext).IsAssignableFrom(typeof(IOptionPricerDbContext)));
        Assert.True(typeof(IOptionPricerDbContext).IsAssignableFrom(typeof(OptionPricerDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_option_pricer_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.OptionPricerDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IOptionPricerDbContext), property.PropertyType);
    }

    [Fact]
    public void Public_context_methods_are_read_or_write_contract_implementations()
    {
        var contractMethods = new[] { typeof(IOptionPricerDbReadContext), typeof(IOptionPricerDbWriteContext) }
            .SelectMany(type => type.GetMethods())
            .ToArray();
        var publicMethods = typeof(OptionPricerDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        Assert.All(publicMethods, implementation =>
            Assert.Contains(contractMethods, contract => SameSignature(contract, implementation)));
    }

    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(OptionPricerDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();

        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);

        var extensions = typeof(OptionPricerDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.OptionPricerDb.OptionPricerDbContextExtensions");
        Assert.NotNull(extensions);
        var extensionMethods = extensions.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(extensionMethods);
        Assert.All(extensionMethods, method =>
            Assert.True(
                method.IsDefined(typeof(ExtensionAttribute), inherit: false),
                $"{method.Name} must be declared as an extension method."));
    }

    [Fact]
    public void Context_is_non_partial_and_extensions_use_csharp_14_blocks()
    {
        var root = FindRepositoryRoot();
        var folder = Path.Combine(root, "TomasAI.IFM.Application.Storage", "OptionPricerDb");
        var context = File.ReadAllText(Path.Combine(folder, "OptionPricerDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "OptionPricerDbContextExtensions.cs"));

        Assert.DoesNotContain("partial class OptionPricerDbContext", context, StringComparison.Ordinal);
        Assert.Contains("extension(OptionPricerDbContext context)", extensions, StringComparison.Ordinal);
        Assert.Contains("extension(SpreadDistributionReadModel distribution)", extensions, StringComparison.Ordinal);
        Assert.Contains("extension(SpreadDistributionJobReadModel job)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
        Assert.Null(typeof(OptionPricerDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.OptionPricerDb.OptionPricerDbException"));
    }

    [Fact]
    public void Extension_methods_have_xml_documentation()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(
            root,
            "TomasAI.IFM.Application.Storage",
            "OptionPricerDb",
            "OptionPricerDbContextExtensions.cs");
        var lines = File.ReadAllLines(path);
        var declarations = lines
            .Select((text, index) => (Text: text, Index: index))
            .Where(line => line.Text.StartsWith("        internal ", StringComparison.Ordinal)
                && line.Text.Contains('('))
            .ToArray();

        Assert.NotEmpty(declarations);
        Assert.All(declarations, declaration =>
        {
            var documentation = lines
                .Take(declaration.Index)
                .Reverse()
                .TakeWhile(line => line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                .ToArray();
            Assert.Contains(documentation, line => line.Contains("<summary>", StringComparison.Ordinal));
            Assert.Contains(documentation, line => line.Contains("</summary>", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Query_results_are_owned_by_the_option_pricer_shared_domain()
    {
        var queryResultTypes = typeof(IOptionPricerDbReadContext)
            .GetMethods()
            .Select(method => UnwrapTask(method.ReturnType))
            .SelectMany(FlattenCollection)
            .Where(type => type != typeof(int))
            .ToArray();

        Assert.NotEmpty(queryResultTypes);
        Assert.All(queryResultTypes, type =>
            Assert.Equal(typeof(OptionPricerDeviceReadModel).Assembly, type.Assembly));
    }

    [Fact]
    public void Persistence_chain_invocations_are_on_separate_lines()
    {
        var root = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(root, "TomasAI.IFM.Application.Storage", "OptionPricerDb", "OptionPricerDbContext.cs"),
            Path.Combine(root, "TomasAI.IFM.Application.Storage", "OptionPricerDb", "OptionPricerDbContextExtensions.cs")
        ];
        string[] boundaries =
        [
            ").SetParameters(", ").ExecuteCommandAsync(", ").ExecuteSingleAsync(", ").ExecuteQueryAsync(",
            ").ExecuteScalarAsync(", ").QueueCommand(", ").ConfigureAwait("
        ];

        foreach (var file in files)
            Assert.DoesNotContain(File.ReadAllLines(file), line =>
                boundaries.Any(boundary => line.Contains(boundary, StringComparison.Ordinal)));
    }

    private static Type UnwrapTask(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)
            ? type.GetGenericArguments()[0]
            : type;

    private static IEnumerable<Type> FlattenCollection(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>)
            ? type.GetGenericArguments()
            : [type];

    private static bool SameSignature(MethodInfo contract, MethodInfo implementation)
        => contract.Name == implementation.Name
            && contract.ReturnType == implementation.ReturnType
            && contract.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(implementation.GetParameters().Select(parameter => parameter.ParameterType));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

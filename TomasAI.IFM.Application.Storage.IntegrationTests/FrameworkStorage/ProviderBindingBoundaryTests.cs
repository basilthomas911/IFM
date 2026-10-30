using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage;

public sealed class ProviderBindingBoundaryTests
{
    [Fact]
    public void Application_storage_leaves_driver_parameter_creation_and_type_mapping_to_providers()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.Application.Storage")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var files = Directory.EnumerateFiles(Path.Combine(directory!.FullName, "TomasAI.IFM.Application.Storage"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
        var forbidden = new Regex(@"\b(NpgsqlParameter|NpgsqlDbType)\b|\.Parameters\.Add(?:Range|WithValue)?\s*\(|\bPostgreSql\s*\{\s*get;", RegexOptions.CultureInvariant);
        Assert.All(files, path => Assert.False(forbidden.IsMatch(File.ReadAllText(path)),
            $"Driver-specific parameter binding leaked into {path}. Pass C# values or BindParameter metadata to the provider."));
    }

    [Fact]
    public void All_binding_contracts_exclude_driver_specific_inputs_and_provider_flags()
    {
        var bindings = typeof(ReferenceDbContext).Assembly.GetTypes()
            .Where(type => typeof(IBindValue).IsAssignableFrom(type) && !type.IsInterface).ToArray();
        Assert.NotEmpty(bindings);
        foreach (var binding in bindings)
        {
            Assert.Null(binding.GetProperty("PostgreSql"));
            var inputs = binding.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters());
            Assert.All(inputs, input => Assert.False(
                input.ParameterType.FullName?.Contains("Npgsql", StringComparison.Ordinal) == true
                || input.ParameterType.FullName?.Contains("Cassandra", StringComparison.Ordinal) == true,
                $"{binding.FullName} takes driver-specific input {input.Name}."));
        }
    }
}

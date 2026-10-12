using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class EventSourceActorDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<EventSourceActorDbContext>)
            .IsAssignableFrom(typeof(IEventSourceActorDbContext)));
        Assert.True(typeof(IEventSourceActorDbReadContext)
            .IsAssignableFrom(typeof(IEventSourceActorDbContext)));
        Assert.True(typeof(IEventSourceActorDbWriteContext)
            .IsAssignableFrom(typeof(IEventSourceActorDbContext)));
        Assert.True(typeof(IEventSourceActorDbContext)
            .IsAssignableFrom(typeof(EventSourceActorDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_event_source_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.ActorEventSourceDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IEventSourceActorDbContext), property.PropertyType);
    }

    [Fact]
    public void Read_and_write_contracts_are_separated_by_behavior()
    {
        Assert.NotNull(typeof(IEventSourceActorDbReadContext)
            .GetMethod(nameof(IEventSourceActorDbReadContext.GetEventLogByEventIdAsync)));
        Assert.Null(typeof(IEventSourceActorDbReadContext)
            .GetMethod(nameof(IEventSourceActorDbWriteContext.SaveEventsAsync)));
        Assert.NotNull(typeof(IEventSourceActorDbWriteContext)
            .GetMethod(nameof(IEventSourceActorDbWriteContext.SaveEventsAsync),
            [typeof(string), typeof(Guid), typeof(DomainEventCollection)]));
        Assert.Null(typeof(IEventSourceActorDbWriteContext)
            .GetMethod(nameof(IEventSourceActorDbReadContext.GetEventLogByEventIdAsync)));
    }
    [Fact]
    public void Context_contains_only_MapTo_static_methods_and_extensions_own_helpers()
    {
        var declared = typeof(EventSourceActorDbContext).GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .ToArray();
        Assert.All(declared.Where(method => method.IsStatic), method =>
            Assert.StartsWith("MapTo", method.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(declared, method => !method.IsPublic && !method.IsStatic);
        var extensions = typeof(EventSourceActorDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.EventSourceDb.EventSourceActorDbContextExtensions");
        Assert.NotNull(extensions);
        var helpers = extensions.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null).ToArray();
        Assert.NotEmpty(helpers);
        Assert.All(helpers, method => Assert.True(method.IsDefined(typeof(ExtensionAttribute), false), method.Name));
    }

    [Fact]
    public void Context_is_non_partial_and_argument_validation_uses_extensions()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "EventSourceDb");
        var context = File.ReadAllText(Path.Combine(folder, "EventSourceActorDbContext.cs"));
        var extensions = File.ReadAllText(Path.Combine(folder, "EventSourceActorDbContextExtensions.cs"));
        Assert.DoesNotContain("partial class EventSourceActorDbContext", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentNullException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException.Throw", context, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentOutOfRangeException.Throw", context, StringComparison.Ordinal);
        Assert.Contains("extension(EventSourceActorDbContext context)", extensions, StringComparison.Ordinal);
        Assert.DoesNotContain("(this ", extensions, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_parameter_records_are_grouped_under_Parameters()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage", "EventSourceDb");
        Assert.DoesNotContain(Directory.GetFiles(folder, "*.cs"), file =>
            File.ReadAllText(file).Contains(": IBindValue", StringComparison.Ordinal));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(folder, "Parameters"), "*.cs"));
        Assert.Empty(typeof(EventSourceActorDbContext).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), false)));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [Fact]
    public void Public_context_methods_implement_read_write_audit_or_lifetime_contracts()
    {
        var contracts = new[] { typeof(IEventSourceActorDbReadContext), typeof(IEventSourceActorDbWriteContext),
            typeof(ICommandAuditLogger), typeof(IDisposable), typeof(IAsyncDisposable) };
        var implementations = contracts.SelectMany(contract => typeof(EventSourceActorDbContext)
            .GetInterfaceMap(contract).TargetMethods).ToHashSet();
        var operations = typeof(EventSourceActorDbContext).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName);
        Assert.All(operations, method => Assert.Contains(method, implementations));
    }

}

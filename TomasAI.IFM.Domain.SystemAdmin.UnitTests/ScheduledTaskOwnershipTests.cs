using System.Reflection;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskOwnershipTests
{
    [Theory]
    [InlineData(typeof(ScheduledTaskCommandActor), "TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command")]
    [InlineData(typeof(ScheduledTaskRunCommandActor), "TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command")]
    [InlineData(typeof(ScheduledTaskCatalogCommandActor), "TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command")]
    public void Each_actor_uses_one_state_and_repository_in_its_own_hierarchy(Type actor, string owner)
    {
        Assert.Equal(owner + ".Actor", actor.Namespace);
        var mapType = actor.GetField("_receiveMap", BindingFlags.Static | BindingFlags.NonPublic)!.FieldType;
        var handler = mapType.GetGenericArguments()[1];
        var state = Assert.Single(handler.GetGenericArguments(), t => typeof(IActorState).IsAssignableFrom(t));
        Assert.Equal(owner + ".State", state.Namespace);
        var context = actor.GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType;
        Assert.Equal(owner + ".Actor", context.Namespace);
        Assert.DoesNotContain(context.GetProperties(), p => typeof(IScheduledTaskReadStore).IsAssignableFrom(p.PropertyType));
        Assert.DoesNotContain(context.GetConstructors().SelectMany(c => c.GetParameters()), p => typeof(IScheduledTaskReadStore).IsAssignableFrom(p.ParameterType));
        Assert.Equal(state, Assert.Single(context.GetProperty("Repository")!.PropertyType.GetGenericArguments()));
        var siblingStates = actor.Assembly.GetTypes().Where(t => t.Namespace == state.Namespace &&
            !t.IsAbstract && typeof(IActorState).IsAssignableFrom(t)).ToArray();
        Assert.Equal(state, Assert.Single(siblingStates));
    }

    [Theory]
    [InlineData(typeof(ScheduledTaskCommandActor))]
    [InlineData(typeof(ScheduledTaskRunCommandActor))]
    [InlineData(typeof(ScheduledTaskCatalogCommandActor))]
    public void Every_mapped_command_has_one_handler_entry_point_with_its_command_owned_helpers(Type actor)
    {
        var supported = (IReadOnlyCollection<Type>)actor.GetProperty("SupportedCommandTypes")!.GetValue(null)!;
        var owner = actor.Namespace![..^".Actor".Length];
        foreach (var command in supported)
        {
            var name = command.Name[..^"Command".Length];
            var handler = actor.Assembly.GetType(owner + "." + name, throwOnError: true)!;
            var method = Assert.Single(handler.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
            Assert.Contains(method.Name, new[] { "Execute", "ExecuteAsync" });
            Assert.Equal(command, method.GetParameters()[0].ParameterType);
            Assert.Equal(typeof(ICommandActorContext<>).MakeGenericType(actor), method.GetParameters()[1].ParameterType);
            var helpers = handler.GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.NotNull(handler.GetMethod("Compute", BindingFlags.NonPublic | BindingFlags.Static));
            var factory = Assert.Single(helpers, m => m.Name.StartsWith("Create", StringComparison.Ordinal) && m.Name.EndsWith("Event", StringComparison.Ordinal));
            Assert.Equal(command, factory.GetParameters()[0].ParameterType);
            Assert.Null(actor.Assembly.GetType(owner + ".Extensions." + name + "Extensions"));
        }
    }

    public static IEnumerable<object[]> MovedEvents() => typeof(ScheduledTaskId).Assembly.GetTypes()
        .Where(t => typeof(IEvent).IsAssignableFrom(t) && t.Namespace is { } ns &&
            (ns.StartsWith("TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events", StringComparison.Ordinal) ||
             ns.StartsWith("TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events", StringComparison.Ordinal)))
        .Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(MovedEvents))]
    public void Moved_events_read_old_names_and_keep_the_existing_persisted_write_identity(Type current)
    {
        var old = current.FullName!.Replace(".ScheduledTask.Run.", ".ScheduledTask.", StringComparison.Ordinal)
            .Replace(".ScheduledTask.Catalog.", ".ScheduledTask.", StringComparison.Ordinal)
            .Replace("ScheduledTaskCatalogProjectRegistered", "ScheduledTaskProjectRegistered", StringComparison.Ordinal)
            .Replace("ScheduledTaskCatalogHostCapabilityRecorded", "ScheduledTaskHostCapabilityRecorded", StringComparison.Ordinal);
        var legacy = old + ", TomasAI.IFM.Application.Contracts.SystemAdmin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        Assert.Equal("ScheduledTaskEvent", current.GetField("Actor")!.GetRawConstantValue());
        var reader = EventTypeRegistry.CreateCompatibilityRegistry();
        Assert.Equal(current, reader.Resolve(legacy));
        Assert.Equal(current, reader.Resolve(old + ", TomasAI.IFM.Domain.SystemAdmin.Shared"));
        var identity = EventWriteIdentityRegistry.Default.GetIdentity(current);
        Assert.Equal(old[(old.LastIndexOf('.') + 1)..], identity.EventName);
        Assert.Equal(old + ", TomasAI.IFM.Domain.SystemAdmin.Shared, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", identity.EventTypeName);
        Assert.Equal(current, reader.Resolve(identity.EventTypeName));
    }

    [Theory]
    [MemberData(nameof(MovedEvents))]
    public void Events_start_with_their_command_owner_name(Type current)
    {
        var prefix = current.Namespace!.Contains(".ScheduledTask.Catalog.", StringComparison.Ordinal)
            ? "ScheduledTaskCatalog" : "ScheduledTaskRun";
        Assert.StartsWith(prefix, current.Name);
    }

    [Theory]
    [MemberData(nameof(MovedEvents))]
    public void Renamed_catalog_events_keep_pre_rename_catalog_identities_and_routing(Type current)
    {
        if (!current.Namespace!.Contains(".ScheduledTask.Catalog.", StringComparison.Ordinal)) return;
        var previous = current.FullName!.Replace("ScheduledTaskCatalogProjectRegistered", "ScheduledTaskProjectRegistered", StringComparison.Ordinal)
            .Replace("ScheduledTaskCatalogHostCapabilityRecorded", "ScheduledTaskHostCapabilityRecorded", StringComparison.Ordinal);
        var reader = EventTypeRegistry.CreateCompatibilityRegistry();
        var historical = previous + ", TomasAI.IFM.Application.Contracts.SystemAdmin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        Assert.Equal(current, reader.Resolve(historical));
        Assert.Contains(historical, reader.GetReadIdentities(current));
        var oldName = previous[(previous.LastIndexOf('.') + 1)..];
        Assert.Equal(oldName[..^"Event".Length], current.GetField("Verb")!.GetRawConstantValue());
        var source = (IEvent)Activator.CreateInstance(current)!;
        var snapshot = new TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts.ScheduledTaskCatalog
        {
            Id = new(Guid.NewGuid()), Revision = 23,
            Projects = [new() { TaskKey = "legacy-deployment", ManifestVersion = "v1", ArtifactDigest = "sha256:legacy" }]
        };
        current.GetProperty("ScheduledTaskCatalog")!.SetValue(source, snapshot);
        foreach (var compressed in new[] { false, true })
        {
            var codec = new TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec(compressed, reader);
            var bytes = codec.Serialize(source);
            var frozen = EventWriteIdentityRegistry.Default.GetIdentity(current);
            foreach (var name in new[] { historical, frozen.EventTypeName, current.AssemblyQualifiedName! })
            {
                var decoded = codec.Deserialize(name, 123, bytes);
                Assert.IsType(current, decoded);
                Assert.Equivalent(snapshot, current.GetProperty("ScheduledTaskCatalog")!.GetValue(decoded));
                Assert.Equal(123, decoded.EventId);
            }
        }
    }
}

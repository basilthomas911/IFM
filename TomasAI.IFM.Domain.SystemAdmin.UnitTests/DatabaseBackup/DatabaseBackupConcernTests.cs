using System.Reflection;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;
public sealed class DatabaseBackupConcernTests
{
    [Fact]
    public void Every_command_actor_dispatches_to_its_own_handlers_and_one_distinct_state()
    {
        var states = new HashSet<Type>();
        foreach (var actor in DatabaseBackupActorInventory.Actors("Command"))
        {
            var handlerNamespace = actor.Namespace![..^".Actor".Length];
            var types = (IReadOnlyCollection<Type>)actor.GetProperty("SupportedCommandTypes")!.GetValue(null)!;
            Type? ownerState = null;
            foreach (var command in types)
            {
                var name = command.Name[..^"Command".Length];
                var handler = actor.Assembly.GetType($"{handlerNamespace}.{name}", true)!;
                var entry = Assert.Single(handler.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
                Assert.Equal("Execute", entry.Name);
                var state = entry.GetParameters()[1].ParameterType;
                Assert.Equal(handlerNamespace + ".State", state.Namespace);
                ownerState ??= state;
                Assert.Equal(ownerState, state);
                Assert.NotNull(handler.GetMethod("Compute", BindingFlags.NonPublic | BindingFlags.Static));
                Assert.NotNull(handler.GetMethod("CreateLifecycleEvents", BindingFlags.NonPublic | BindingFlags.Static));
            }
            Assert.True(states.Add(ownerState!));
            var methods = ownerState!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.DoesNotContain(methods, m => m.Name is "Compute" or "Execute");
        }
        Assert.Equal(6, states.Count);
    }
    [Fact]
    public void Every_operator_command_and_query_routes_directly_to_its_concern_actor()
    {
        foreach (var type in DatabaseBackupActorInventory.CommandTypes.Where(t => typeof(IDatabaseBackupCommand).IsAssignableFrom(t)))
        {
            var command = (IDatabaseBackupCommand)Activator.CreateInstance(type)!;
            var route = DatabaseBackupCommandRoute.For(command);
            var actor = Assert.Single(DatabaseBackupActorInventory.Actors("Command"), a => (string)a.GetField("Actor")!.GetRawConstantValue()! == route);
            Assert.Contains(type, (IReadOnlyCollection<Type>)actor.GetProperty("SupportedCommandTypes")!.GetValue(null)!);
        }
        foreach (var type in DatabaseBackupActorInventory.QueryTypes)
        {
            var query = (IDatabaseBackupQuery)Activator.CreateInstance(type)!;
            var route = DatabaseBackupQueryRoute.For(query);
            var actor = Assert.Single(DatabaseBackupActorInventory.Actors("Query"), a => (string)a.GetField("Actor")!.GetRawConstantValue()! == route);
            Assert.Contains(type, (IReadOnlyCollection<Type>)actor.GetProperty("SupportedQueryTypes")!.GetValue(null)!);
        }
    }
    [Fact]
    public void Service_observations_have_exactly_one_concern_event_actor()
    {
        foreach (var type in DatabaseBackupActorInventory.EventTypes)
        {
            var e = (DatabaseBackupServiceEventContract)Activator.CreateInstance(type)!;
            var route = DatabaseBackupServiceEventRoute.For(e.Verb, DatabaseRecoveryOperationKind.Backup);
            var actor = Assert.Single(DatabaseBackupActorInventory.Actors("Event"), a => (string)a.GetField("Actor")!.GetRawConstantValue()! == route);
            Assert.Contains(type, (IReadOnlyCollection<Type>)actor.GetProperty("SupportedServiceEventTypes")!.GetValue(null)!);
        }
    }
    [Theory]
    [InlineData(DatabaseRecoveryOperationKind.Backup, "DatabaseBackupEvent")]
    [InlineData(DatabaseRecoveryOperationKind.Restore, "DatabaseRecoveryEvent")]
    [InlineData(DatabaseRecoveryOperationKind.Cutover, "DatabaseRecoveryEvent")]
    [InlineData(DatabaseRecoveryOperationKind.Retention, "DatabaseBackupRetentionEvent")]
    [InlineData(DatabaseRecoveryOperationKind.Reconciliation, "DatabaseBackupPolicyEvent")]
    public void Generic_observations_share_the_operation_mailbox_with_its_other_receipts(DatabaseRecoveryOperationKind kind, string expected)
    {
        foreach (var verb in new[] { "RunStatisticsCaptured", "ServiceReconciliation" })
        {
            Assert.Equal(expected, DatabaseBackupServiceEventRoute.For(verb, kind));
            var actor = Assert.Single(DatabaseBackupActorInventory.Actors("Event"), a => (string)a.GetField("Actor")!.GetRawConstantValue()! == expected);
            Assert.Contains(verb, (IReadOnlyCollection<string>)actor.GetProperty("SupportedVerbs")!.GetValue(null)!);
        }
    }
    [Theory]
    [InlineData(DatabaseRecoveryOperationKind.Backup, "DatabaseBackupCommand")]
    [InlineData(DatabaseRecoveryOperationKind.Verification, "DatabaseBackupCommand")]
    [InlineData(DatabaseRecoveryOperationKind.Restore, "DatabaseRecoveryCommand")]
    [InlineData(DatabaseRecoveryOperationKind.RestoreDrill, "DatabaseRecoveryCommand")]
    [InlineData(DatabaseRecoveryOperationKind.Cutover, "DatabaseRecoveryCommand")]
    [InlineData(DatabaseRecoveryOperationKind.Retention, "DatabaseBackupRetentionCommand")]
    [InlineData(DatabaseRecoveryOperationKind.Reconciliation, "DatabaseBackupPolicyCommand")]
    public void Lifecycle_receipts_stay_with_the_owner_of_the_operation(DatabaseRecoveryOperationKind kind, string expected)
    {
        var command = new RecordDatabaseOperationProgressCommand { Source = new() { OperationKind = kind } };
        Assert.Equal(expected, DatabaseBackupCommandRoute.For(command));
        var actor = Assert.Single(DatabaseBackupActorInventory.Actors("Command"), a => (string)a.GetField("Actor")!.GetRawConstantValue()! == expected);
        Assert.Contains(command.GetType(), (IReadOnlyCollection<Type>)actor.GetProperty("SupportedCommandTypes")!.GetValue(null)!);
    }
}

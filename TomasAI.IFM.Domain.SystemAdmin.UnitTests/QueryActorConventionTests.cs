using System.Collections;
using System.Reflection;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Lifecycle.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class QueryActorConventionTests
{
    [Theory]
    [InlineData(typeof(DatabaseBackupQueryActor), 3)]
    [InlineData(typeof(DatabaseBackupSetQueryActor), 1)]
    [InlineData(typeof(DatabaseBackupLifecycleQueryActor), 1)]
    [InlineData(typeof(DatabaseBackupPolicyQueryActor), 2)]
    [InlineData(typeof(DatabaseRecoveryQueryActor), 7)]
    [InlineData(typeof(DatabaseBackupRetentionQueryActor), 1)]
    [InlineData(typeof(DatabaseBackupServiceQueryActor), 2)]
    [InlineData(typeof(ScheduledTaskQueryActor), 10)]
    public void Every_mapped_query_has_one_typed_read_only_extension(Type actor, int expectedQueries)
    {
        var receive = Map(actor, "_receiveMap");
        var parse = Map(actor, "_parseMap");
        var exceptions = Map(actor, "_exceptionMap");
        Assert.Equal(expectedQueries, parse.Count);
        Assert.Equal(expectedQueries, receive.Count);
        Assert.True(receive.Keys.Cast<Type>().ToHashSet().SetEquals(exceptions.Keys.Cast<Type>()));

        Type[] readDependencies =
        [
            typeof(ISystemAdminDbReadContext), typeof(ITradeDbReadContext), typeof(IScheduledTaskReadStore),
            typeof(IDatabaseBackupOutputReader), typeof(IScheduledTaskOutputReader),
            typeof(TimeProvider), typeof(CancellationToken)
        ];
        foreach (var query in receive.Keys.Cast<Type>())
        {
            var queryInterface = Assert.Single(query.GetInterfaces(),
                candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IQuery<>));
            var result = typeof(ServiceResult<>).MakeGenericType(queryInterface.GetGenericArguments());
            var handler = Assert.Single(actor.Assembly.GetTypes(),
                type => type.Name == query.Name[..^"Query".Length] &&
                    type.Namespace?.EndsWith(".Query", StringComparison.Ordinal) == true);
            var entry = handler.GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            var parameters = entry.GetParameters();
            Assert.Equal(typeof(ValueTask<>).MakeGenericType(result), entry.ReturnType);
            Assert.Equal(query, parameters[0].ParameterType);
            Assert.All(parameters.Skip(1), parameter => Assert.Contains(parameter.ParameterType, readDependencies));
        }
    }

    static IDictionary Map(Type actor, string field)
        => Assert.IsAssignableFrom<IDictionary>(actor.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
}

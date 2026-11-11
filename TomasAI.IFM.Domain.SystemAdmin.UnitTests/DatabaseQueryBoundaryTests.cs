using System.Reflection;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class DatabaseQueryBoundaryTests
{
    public static IEnumerable<object[]> QueryHandlers => typeof(GetDatabaseProtectionSets).Assembly.GetTypes()
        .Where(type => type.IsAbstract && type.IsSealed && type.Namespace?.Contains(".Query", StringComparison.Ordinal) == true)
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        .Where(method => method.Name == "ExecuteAsync" && method.GetParameters().Length > 1
            && typeof(IDatabaseBackupQuery).IsAssignableFrom(method.GetParameters()[0].ParameterType))
        .Select(method => new object[] { method.DeclaringType!.FullName! });

    [Theory]
    [MemberData(nameof(QueryHandlers))]
    public async Task Null_queries_are_rejected_by_the_domain_before_persistence(string handler)
    {
        var method = typeof(GetDatabaseProtectionSets).Assembly.GetType(handler)!.GetMethod("ExecuteAsync")!;
        var database = Substitute.For<ISystemAdminDbReadContext>();
        var arguments = method.GetParameters().Select(parameter => parameter.ParameterType == typeof(ISystemAdminDbReadContext)
            ? (object)database : parameter.ParameterType == typeof(CancellationToken) ? CancellationToken.None : null).ToArray();
        var exception = await Record.ExceptionAsync(async () =>
        {
            try
            {
                var result = method.Invoke(null, arguments)!;
                await (Task)result.GetType().GetMethod("AsTask")!.Invoke(result, null)!;
            }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        });
        Assert.IsType<ArgumentNullException>(exception);
        Assert.Empty(database.ReceivedCalls());
    }

    [Fact]
    public void Boundary_cases_include_every_storage_query_handler()
        => Assert.True(QueryHandlers.Count() >= 13, "The caller-validation suite must not silently omit the query handlers.");
}

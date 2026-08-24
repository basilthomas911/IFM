using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

#pragma warning disable NS1004

public sealed class DatabaseBackupQueryActorTests
{
    sealed class TestableQueryActor(ISystemAdminDbContext db, ILogger<DatabaseBackupQueryActor> logger)
        : DatabaseBackupQueryActor(new DatabaseBackupQueryContext(Substitute.For<IActorSupervisor>(), db, logger))
    {
        public ValueTask Receive(IQueryActorContext context, IQuery query, CancellationToken cancellationToken)
            => base.ReceiveAsync(context, query, cancellationToken);
    }

    [Fact]
    public async Task Query_handler_validates_reads_projection_and_replies_with_typed_result()
    {
        var db = Substitute.For<ISystemAdminDbContext>();
        var context = Substitute.For<IQueryActorContext>();
        var rows = new[] { new DatabaseBackupHealthReadModel { Source = BackupSource.LocalWorkstation, Ready = true } };
        var query = new GetDatabaseBackupServiceHealthQuery
        {
            EntityId = new DatabaseRecoveryOperationId(Guid.NewGuid()), Request = Request(),
            Subject = new ActorSubject(ActorType.Query, DatabaseBackupQuery.Actor, "GetServiceHealth", Guid.NewGuid().ToString("N"))
        };
        db.GetServiceHealthAsync(query, CancellationToken.None).Returns(ValueTask.FromResult(rows));
        var actor = new TestableQueryActor(db, Substitute.For<ILogger<DatabaseBackupQueryActor>>());

        await actor.Receive(context, query, CancellationToken.None);

        await db.Received(1).GetServiceHealthAsync(query, CancellationToken.None);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId, query.Verb,
            Arg.Is<ServiceResult<DatabaseBackupHealthReadModel[]>>(result => result.Success && result.Value == rows));
    }

    [Fact]
    public async Task Missing_single_projection_returns_typed_not_found_result()
    {
        var db = Substitute.For<ISystemAdminDbContext>();
        var context = Substitute.For<IQueryActorContext>();
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var query = new GetDatabaseBackupOperationQuery
        {
            EntityId = operationId, OperationId = operationId, Request = Request(),
            Subject = new ActorSubject(ActorType.Query, DatabaseBackupQuery.Actor, "GetBackupOperation", operationId.Format())
        };
        db.GetBackupOperationAsync(query, CancellationToken.None).Returns(ValueTask.FromResult<DatabaseBackupOperationReadModel?>(null));
        var actor = new TestableQueryActor(db, Substitute.For<ILogger<DatabaseBackupQueryActor>>());

        await actor.Receive(context, query, CancellationToken.None);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId, query.Verb,
            Arg.Is<ServiceResult<DatabaseBackupOperationReadModel>>(result => !result.Success && result.ErrorCode == 404));
    }

    static DatabaseRequestEnvelope Request() => new()
    {
        RequestId = Guid.NewGuid(), CallerIdentity = "operator", AuthorizationReference = "approval",
        CallerRoles = ["DatabaseRecoveryReader"], Origin = DatabaseRequestOrigin.UI,
        CorrelationId = Guid.NewGuid(), EnvironmentIdentity = "paper-trading", CreatedUtc = DateTimeOffset.UtcNow
    };
}

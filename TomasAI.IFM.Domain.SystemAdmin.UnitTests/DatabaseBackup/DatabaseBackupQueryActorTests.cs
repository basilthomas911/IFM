using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Query.Actor;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Lifecycle.Query.Actor;
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
        public ValueTask Receive(IQueryActorContext<DatabaseBackupQueryActor> context, IQuery query, CancellationToken cancellationToken)
            => base.ReceiveAsync(context, query, cancellationToken);
    }

    sealed class TestableServiceQueryActor(ISystemAdminDbContext db, ILogger<DatabaseBackupServiceQueryActor> logger)
        : DatabaseBackupServiceQueryActor(new DatabaseBackupServiceQueryContext(Substitute.For<IActorSupervisor>(), db, logger))
    {
        public ValueTask Receive(IQueryActorContext<DatabaseBackupServiceQueryActor> context, IQuery query, CancellationToken cancellationToken)
            => base.ReceiveAsync(context, query, cancellationToken);
    }

    sealed class TestableLifecycleQueryActor(IDatabaseBackupLifecycleQueryContext context)
        : DatabaseBackupLifecycleQueryActor(context)
    {
        public ValueTask Receive(CancellationToken cancellationToken, IQuery query)
            => base.ReceiveAsync(context, query, cancellationToken);
    }

    [Fact]
    public async Task Query_handler_validates_reads_projection_and_replies_with_typed_result()
    {
        var db = Substitute.For<ISystemAdminDbContext>();
        var context = Substitute.For<IQueryActorContext<DatabaseBackupServiceQueryActor>>();
        var rows = new[] { new DatabaseBackupHealthReadModel { Source = BackupSource.LocalWorkstation, Ready = true } };
        var query = new GetDatabaseBackupServiceHealthQuery
        {
            EntityId = new DatabaseRecoveryOperationId(Guid.NewGuid()),
            Request = Request(),
            Subject = new ActorSubject(ActorType.Query, DatabaseBackupQueryRoute.Service, "GetServiceHealth", Guid.NewGuid().ToString("N"))
        };
        var projection = new TaskCompletionSource<DatabaseBackupHealthReadModel[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        db.GetServiceHealthAsync(Arg.Any<GetDatabaseBackupServiceHealthQuery>(), CancellationToken.None)
            .Returns(new ValueTask<DatabaseBackupHealthReadModel[]>(projection.Task));
        var actor = new TestableServiceQueryActor(db, Substitute.For<ILogger<DatabaseBackupServiceQueryActor>>());

        var handling = actor.Receive(context, query, CancellationToken.None);
        await context.DidNotReceive().ReplyAsync(
            query.Subject.ThreadId, query.Verb, Arg.Any<ServiceResult<DatabaseBackupHealthReadModel[]>>());
        projection.SetResult(rows);
        await handling;

        await db.Received(1).GetServiceHealthAsync(
            Arg.Is<GetDatabaseBackupServiceHealthQuery>(legacy => legacy.EntityId == query.EntityId && legacy.Request.RequestId == query.Request.RequestId),
            CancellationToken.None);
        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId, query.Verb,
            Arg.Is<ServiceResult<DatabaseBackupHealthReadModel[]>>(result => result.Success && result.Value == rows));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Backup_log_reply_reads_retained_output_only_for_the_projected_source(bool sourceMatches)
    {
        var db = Substitute.For<ISystemAdminDbContext>();
        var reader = Substitute.For<IDatabaseBackupOutputReader>();
        var context = Substitute.For<IDatabaseBackupLifecycleQueryContext>();
        context.DbContext.Returns(db);
        context.OutputReader.Returns(reader);
        context.Logger.Returns(Substitute.For<ILogger<DatabaseBackupLifecycleQueryActor>>());
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var query = new GetDatabaseBackupLogQuery
        {
            EntityId = operationId,
            OperationId = operationId,
            Request = Request(),
            Source = BackupSource.LocalWorkstation,
            Subject = new(ActorType.Query, DatabaseBackupLifecycleQueryActor.Actor, "GetBackupLog", operationId.Format()),
            OutputOffset = 32
        };
        db.GetBackupOperationAsync(Arg.Is<GetDatabaseBackupOperationQuery>(request => request.OperationId == operationId), CancellationToken.None)
            .Returns(ValueTask.FromResult<DatabaseBackupOperationReadModel?>(new()
            {
                OperationId = operationId,
                Source = sourceMatches ? query.Source : default
            }));
        DatabaseBackupPhaseReadModel[] phases = [new()];
        db.GetBackupPhasesAsync(query, CancellationToken.None).Returns(ValueTask.FromResult(phases));
        var retained = new DatabaseBackupLogReadModel { Output = "retained output", NextOutputOffset = 47, OutputAvailable = true };
        reader.ReadAsync(query.Source, operationId.Value, query.OutputOffset, CancellationToken.None).Returns(ValueTask.FromResult(retained));

        await new TestableLifecycleQueryActor(context).Receive(CancellationToken.None, query);

        if (sourceMatches)
        {
            var expected = retained with { Phases = phases };
            await reader.Received(1).ReadAsync(query.Source, operationId.Value, query.OutputOffset, CancellationToken.None);
            await context.Received(1).ReplyAsync(query.Subject.ThreadId, query.Verb,
                Arg.Is<ServiceResult<DatabaseBackupLogReadModel>>(result => result.Success && result.Value == expected));
        }
        else
        {
            await reader.DidNotReceive().ReadAsync(query.Source, operationId.Value, query.OutputOffset, CancellationToken.None);
            await context.Received(1).ReplyAsync(query.Subject.ThreadId, query.Verb,
                Arg.Is<ServiceResult<DatabaseBackupLogReadModel>>(result => !result.Success && result.ErrorCode == 404));
        }
    }

    [Fact]
    public async Task Missing_single_projection_returns_typed_not_found_result()
    {
        var db = Substitute.For<ISystemAdminDbContext>();
        var context = Substitute.For<IQueryActorContext<DatabaseBackupQueryActor>>();
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var query = new GetDatabaseBackupOperationQuery
        {
            EntityId = operationId,
            OperationId = operationId,
            Request = Request(),
            Subject = new ActorSubject(ActorType.Query, DatabaseBackupQueryRoute.Actor, "GetBackupOperation", operationId.Format())
        };
        db.GetBackupOperationAsync(Arg.Any<GetDatabaseBackupOperationQuery>(), CancellationToken.None).Returns(ValueTask.FromResult<DatabaseBackupOperationReadModel?>(null));
        var actor = new TestableQueryActor(db, Substitute.For<ILogger<DatabaseBackupQueryActor>>());

        await actor.Receive(context, query, CancellationToken.None);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId, query.Verb,
            Arg.Is<ServiceResult<DatabaseBackupOperationReadModel>>(result => !result.Success && result.ErrorCode == 404));
    }

    static DatabaseRequestEnvelope Request() => new()
    {
        RequestId = Guid.NewGuid(),
        CallerIdentity = "operator",
        AuthorizationReference = "approval",
        CallerRoles = ["DatabaseRecoveryReader"],
        Origin = DatabaseRequestOrigin.UI,
        CorrelationId = Guid.NewGuid(),
        EnvironmentIdentity = "paper-trading",
        CreatedUtc = DateTimeOffset.UtcNow
    };
}

using NSubstitute;
using FluentAssertions;
using MessagePack;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

#pragma warning disable NS1004 // Matchers inspect immutable message properties on an interface substitute call.

public sealed class DatabaseBackupApiTests
{
    [Fact]
    public void Client_apis_register_independently_as_scoped_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IActorProducer>());
        services.AddDatabaseBackupNatsClientApis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDatabaseBackupCommandApi>().Should().BeOfType<DatabaseBackupCommandApi>();
        scope.ServiceProvider.GetRequiredService<IDatabaseBackupQueryApi>().Should().BeOfType<DatabaseBackupQueryApi>();
    }

    [Fact]
    public async Task Command_api_validates_normalizes_and_routes_request_reply()
    {
        var producer = Substitute.For<IActorProducer>();
        var accepted = new DatabaseOperationAcceptedResult
        {
            OperationId = new DatabaseRecoveryOperationId(Guid.NewGuid()),
            Source = BackupSource.LocalWorkstation,
            PolicyRevision = 4,
            InitialPhase = DatabaseRecoveryPhase.Requested
        };
        producer.RequestAsync<RequestDatabaseBackupCommand, DatabaseRecoveryOperationId, GuidResult>(
                Arg.Any<ActorSubject>(), Arg.Any<RequestDatabaseBackupCommand>(), Arg.Any<DatabaseRecoveryOperationId>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(accepted.OperationId.Value))));
        var api = new DatabaseBackupCommandApi(producer);
        var requestId = Guid.NewGuid();
        var command = new RequestDatabaseBackupCommand
        {
            Request = ValidRequest(requestId),
            Source = BackupSource.LocalWorkstation,
            ProtectionSetId = new DatabaseProtectionSetId("core"),
            ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
            RequiredDestinations = [new DatabaseLogicalDestination("online-vault", true)],
            ExpectedPolicyRevision = 4
        };

        var result = await api.RequestBackupAsync(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(accepted);
        await producer.Received(1).RequestAsync<RequestDatabaseBackupCommand, DatabaseRecoveryOperationId, GuidResult>(
            Arg.Is<ActorSubject>(subject => subject.ActorType == ActorType.Command && subject.Name == DatabaseBackupCommandRoute.Actor && subject.Verb == command.Verb),
            Arg.Is<RequestDatabaseBackupCommand>(sent => sent.CommandId == requestId && sent.EntityId.Value == requestId),
            Arg.Is<DatabaseRecoveryOperationId>(id => id.Value == requestId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Query_api_supplies_query_subject_and_preserves_cancellation()
    {
        var producer = Substitute.For<IActorProducer>();
        var rows = new[] { new DatabaseBackupHealthReadModel { Source = BackupSource.LocalWorkstation } };
        producer.RequestAsync<DatabaseBackupHealthReadModel[], GetDatabaseBackupServiceHealthQuery>(
                Arg.Any<ActorSubject>(), Arg.Any<GetDatabaseBackupServiceHealthQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseBackupHealthReadModel[]>>(new ServiceOk<DatabaseBackupHealthReadModel[]>(rows)));
        var api = new DatabaseBackupQueryApi(producer);
        var requestId = Guid.NewGuid();

        var result = await api.GetServiceHealthAsync(new GetDatabaseBackupServiceHealthQuery { Request = ValidRequest(requestId) }, CancellationToken.None);

        result.Value.Should().BeEquivalentTo(rows);
        await producer.Received(1).RequestAsync<DatabaseBackupHealthReadModel[], GetDatabaseBackupServiceHealthQuery>(
            Arg.Is<ActorSubject>(subject => subject.ActorType == ActorType.Query && subject.Name == DatabaseBackupQueryRoute.Actor && subject.Verb == "GetServiceHealth"),
            Arg.Is<GetDatabaseBackupServiceHealthQuery>(query => query.EntityId.Value == requestId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Protection_set_query_uses_canonical_direct_wire_route()
    {
        var producer = Substitute.For<IActorProducer>();
        var rows = Array.Empty<DatabaseProtectionSetReadModel>();
        producer.RequestAsync<DatabaseProtectionSetReadModel[], GetDatabaseProtectionSetsQuery>(
                Arg.Any<ActorSubject>(), Arg.Any<GetDatabaseProtectionSetsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>>(
                new ServiceOk<DatabaseProtectionSetReadModel[]>(rows)));
        var requestId = Guid.NewGuid();
        var api = new DatabaseBackupQueryApi(producer);

        var result = await api.GetProtectionSetsAsync(
            new GetDatabaseProtectionSetsQuery { Request = ValidRequest(requestId) });

        result.Success.Should().BeTrue();
        await producer.Received(1).RequestAsync<DatabaseProtectionSetReadModel[], GetDatabaseProtectionSetsQuery>(
            Arg.Is<ActorSubject>(subject => subject.Name == DatabaseBackupQueryRoute.Actor && subject.Verb == "GetProtectionSets"),
            Arg.Is<GetDatabaseProtectionSetsQuery>(query => query.EntityId.Value == requestId),
            Arg.Any<CancellationToken>());
        var keys = typeof(GetDatabaseProtectionSetsQuery)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey, property.Name))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, 14));
        keys[0].Name.Should().Be("Subject");
        keys[1].Name.Should().Be("EntityId");
        var query = new GetDatabaseProtectionSetsQuery { Request = ValidRequest(requestId) };
        MessagePackSerializer.Deserialize<GetDatabaseProtectionSetsQuery>(
            MessagePackSerializer.Serialize(query)).Request.RequestId.Should().Be(requestId);
    }

    [Fact]
    public async Task Command_api_rejects_invalid_source_without_sending()
    {
        var producer = Substitute.For<IActorProducer>();
        var api = new DatabaseBackupCommandApi(producer);
        var command = new RequestDatabaseBackupCommand
        {
            Request = ValidRequest(Guid.NewGuid()),
            Source = BackupSource.None,
            ProtectionSetId = new DatabaseProtectionSetId("core"),
            ConsistencyMode = DatabaseConsistencyMode.EngineConsistent,
            RequiredDestinations = [new DatabaseLogicalDestination("vault", true)]
        };

        var result = await api.RequestBackupAsync(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        await producer.DidNotReceiveWithAnyArgs().RequestAsync<RequestDatabaseBackupCommand, DatabaseRecoveryOperationId, GuidResult>(default, default!, default, default);
    }

    static DatabaseRequestEnvelope ValidRequest(Guid requestId) => new()
    {
        RequestId = requestId,
        CallerIdentity = "operator",
        AuthorizationReference = "approval",
        CallerRoles = ["DatabaseRecoveryOperator"],
        Origin = DatabaseRequestOrigin.Console,
        CorrelationId = Guid.NewGuid(),
        EnvironmentIdentity = "paper-trading",
        CreatedUtc = DateTimeOffset.UtcNow
    };
}

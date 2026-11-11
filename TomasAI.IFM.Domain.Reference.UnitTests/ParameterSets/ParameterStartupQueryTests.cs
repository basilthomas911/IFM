using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterStartupQueryTests
{
    [Fact]
    public async Task Serialized_exact_query_returns_only_the_requested_run()
    {
        var requested = CreateRun(Guid.NewGuid(), new DateTime(2026, 9, 16, 12, 1, 0, DateTimeKind.Utc));
        var context = Context();
        context.ConfigurationDb.GetActiveParameterStartupAsync(requested.RunId, CancellationToken.None)
            .Returns(new ParameterStartupReadModel(requested, null));
        var query = MessagePackSerializer.Deserialize<GetParameterStartupRunQuery>(
            MessagePackSerializer.Serialize(new GetParameterStartupRunQuery
            {
                RunId = requested.RunId,
                Subject = new(
                    ActorType.Query,
                    GetParameterStartupRunQuery.Actor,
                    GetParameterStartupRunQuery.Verb,
                    ActorEntityId.Default.Format())
            }));

        await ReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetParameterStartupRunQuery.Verb,
            Arg.Is<ServiceResult<ParameterStartupRun>>(
                result => result.Success && result.Value == requested));
    }

    [Fact]
    public async Task History_query_returns_a_bounded_cursor_page()
    {
        var first = CreateRun(Guid.NewGuid(), new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
        var second = CreateRun(Guid.NewGuid(), new DateTime(2026, 9, 16, 12, 1, 0, DateTimeKind.Utc));
        var third = CreateRun(Guid.NewGuid(), new DateTime(2026, 9, 16, 12, 2, 0, DateTimeKind.Utc));
        var context = Context();
        context.ConfigurationDb.GetActiveParameterStartupPageAsync(2, first.CreatedAtUtc, first.RunId, CancellationToken.None)
            .Returns(new[] { second, third });
        var query = new GetParameterStartupRunsQuery
        {
            Limit = 2,
            AfterCreatedAtUtc = first.CreatedAtUtc,
            AfterRunId = first.RunId,
            Subject = new(
                ActorType.Query,
                GetParameterStartupRunsQuery.Actor,
                GetParameterStartupRunsQuery.Verb,
                ActorEntityId.Default.Format())
        };

        await ReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetParameterStartupRunsQuery.Verb,
            Arg.Is<ServiceResult<ParameterStartupRun[]>>(
                result => result.Success
                    && result.Value!.Select(run => run.RunId).SequenceEqual(
                        new[] { second.RunId, third.RunId })));
    }

    [Fact]
    public async Task Missing_exact_run_fails_without_sending_a_reply()
    {
        var context = Context();
        context.ConfigurationDb.GetActiveParameterStartupAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ParameterStartupReadModel(null, null));
        var query = new GetParameterStartupRunQuery
        {
            RunId = Guid.NewGuid(),
            Subject = new(
                ActorType.Query,
                GetParameterStartupRunQuery.Actor,
                GetParameterStartupRunQuery.Verb,
                ActorEntityId.Default.Format())
        };

        var action = async () => await ReceiveAsync(context, query);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PARAM.STARTUP_NOT_FOUND");
        await context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<ParameterStartupRun>)!);
    }

    internal static ValueTask ReceiveAsync(IParameterSetQueryContext context, IQuery query)
    {
        var receive = typeof(ParameterSetQueryActor).GetMethod(
            "ReceiveAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            [typeof(IQueryActorContext<ParameterSetQueryActor>), typeof(IQuery), typeof(CancellationToken)])!;
        return (ValueTask)receive.Invoke(new ParameterSetQueryActor(context), [context, query, CancellationToken.None])!;
    }

    static IParameterSetQueryContext Context()
    {
        var context = Substitute.For<IParameterSetQueryContext>();
        context.Logger.Returns(Substitute.For<ILogger<ParameterSetQueryActor>>());
        context.AccessPolicy.Returns(Substitute.For<IParameterAccessPolicy>());
        return context;
    }

    static ParameterStartupRun CreateRun(Guid runId, DateTime createdAtUtc)
    {
        var snapshot = new ParameterStartupSnapshotModel(
            runId,
            [],
            new Dictionary<ParameterVersionRef, ParameterSetVersion>());
        var plan = SignalStartupPlanModel.Create(
            snapshot,
            SignalStartupPlanModel.ExistingIntradayConsumers());
        return new ParameterStartupRun(runId, [], [], plan, createdAtUtc, "test");
    }
}

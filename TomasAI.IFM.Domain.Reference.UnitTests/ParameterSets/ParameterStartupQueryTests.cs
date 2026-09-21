using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterStartupQueryTests
{
    [Fact]
    public async Task Serialized_exact_query_returns_only_the_requested_run()
    {
        var first = CreateRun(Guid.NewGuid(), new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
        var requested = CreateRun(Guid.NewGuid(), new DateTime(2026, 9, 16, 12, 1, 0, DateTimeKind.Utc));
        var state = new ParameterStartupCommandState();
        state.ActiveRuns.Add(first.RunId, first);
        state.ActiveRuns.Add(requested.RunId, requested);
        var context = Context(state);
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

        await query.ExecuteAsync(context, context.Logger, CancellationToken.None);

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
        var state = new ParameterStartupCommandState();
        state.ActiveRuns.Add(first.RunId, first);
        state.ActiveRuns.Add(second.RunId, second);
        state.ActiveRuns.Add(third.RunId, third);
        var context = Context(state);
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

        await query.ExecuteAsync(context, context.Logger, CancellationToken.None);

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
        var context = Context(new ParameterStartupCommandState());
        var query = new GetParameterStartupRunQuery
        {
            RunId = Guid.NewGuid(),
            Subject = new(
                ActorType.Query,
                GetParameterStartupRunQuery.Actor,
                GetParameterStartupRunQuery.Verb,
                ActorEntityId.Default.Format())
        };

        var action = async () => await query.ExecuteAsync(
            context,
            context.Logger,
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("PARAM.STARTUP_NOT_FOUND");
        await context.DidNotReceiveWithAnyArgs().ReplyAsync(
            default,
            default!,
            default(ServiceResult<ParameterStartupRun>)!);
    }

    static IParameterSetQueryContext Context(ParameterStartupCommandState state)
    {
        var context = Substitute.For<IParameterSetQueryContext>();
        context.Logger.Returns(Substitute.For<ILogger<ParameterSetQueryActor>>());
        context.AccessPolicy.Returns(Substitute.For<IParameterAccessPolicy>());
        context.Startups.LoadStateAsync(Arg.Any<ICommand>())
            .Returns(new ValueTask<ParameterStartupCommandState>(state));
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

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.IntegrationTests.ParameterSets;

[Collection(ReferenceIntegrationInfrastructureCollection.Name)]
public sealed class ParameterSetActorRuntimeTests(ReferenceIntegrationInfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Global_cache_policy_actor_publication_projects_exact_payload_into_scylla()
    {
        var api = new ParameterSetsApi(infrastructure.ActorProducer);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var id = Guid.NewGuid();
        var payload = TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.IronCondor(id, Guid.Empty, 0);
        var created = await api.CreateAsync(new()
        {
            CommandId = Guid.NewGuid(), EntityId = new(id),
            ComponentCode = "market-data.strategy-option-chain-cache", Name = payload.Name,
            SchemaVersion = 1, PayloadJson = payload.Serialize()
        }, deadline.Token);
        Assert.True(created.Success, created.ErrorMessage);
        var command = new PublishParameterVersionCommand
        { CommandId = Guid.NewGuid(), EntityId = new(id), Version = 1, ExpectedRevision = 1, ComponentCode = "market-data.strategy-option-chain-cache",
            Name = payload.Name, SchemaVersion = 1, PayloadJson = payload.Serialize() };
        var published = await api.PublishAsync(command, deadline.Token);
        Assert.True(published.Success, published.ErrorMessage);
        Assert.True((await api.PublishAsync(command, deadline.Token)).Success);
        var persisted = await infrastructure.MarketDataDb.ReadVersionAsync(id, 1, deadline.Token);
        // Command acceptance precedes asynchronous read-model projection.
        while (persisted is null && !deadline.IsCancellationRequested)
        {
            await Task.Delay(100, deadline.Token);
            persisted = await infrastructure.MarketDataDb.ReadVersionAsync(id, 1, deadline.Token);
        }
        Assert.NotNull(persisted); Assert.Equal(payload.Hash(), persisted.Hash());
        Assert.DoesNotContain(await infrastructure.MarketDataDb.ReadPublishedAsync("Development", deadline.Token), p => p.ParameterSetId == id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Real_actor_routes_assign_and_disable_option_spread_defaults(bool ironCondor)
    {
        var api = new ParameterSetsApi(infrastructure.ActorProducer);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var token = deadline.Token;
        var setId = Guid.NewGuid();
        IParameterComponentDescriptor descriptor = ironCondor
            ? new IronCondorMarketSelectionParameterModel()
            : new VerticalSpreadMarketSelectionParameterModel();
        var scope = ironCondor
            ? OptionSpreadStrategyParameterScopeModel.IronCondor()
            : OptionSpreadStrategyParameterScopeModel.VerticalSpread();
        var identity = new ParameterAssignmentEntityId(ParameterAssignmentPolicyModel.AssignmentId(scope));

        var created = await api.CreateAsync(new()
        {
            CommandId = Guid.NewGuid(),
            EntityId = new(setId),
            ComponentCode = descriptor.Summary.ComponentCode,
            Name = descriptor.Summary.Name,
            SchemaVersion = descriptor.Summary.SchemaVersions.Single(),
            PayloadJson = descriptor.CreateDraftPayload(setId)
        }, token);
        Assert.True(created.Success, created.ErrorMessage);
        var versionDraft = (await api.StateAsync(setId, token)).Value!.Versions.Single();
        var published = await api.PublishAsync(new()
        {
            CommandId = Guid.NewGuid(), EntityId = new(setId), Version = 1, ExpectedRevision = 1,
            ComponentCode = versionDraft.Reference.ComponentCode, Name = versionDraft.Name,
            Description = versionDraft.Description, SchemaVersion = versionDraft.SchemaVersion,
            PayloadJson = versionDraft.PayloadJson
        }, token);
        Assert.True(published.Success, published.ErrorMessage);
        var version = (await api.StateAsync(setId, token)).Value!.Versions.Single();

        var assigned = await api.AssignAsync(new()
        {
            CommandId = Guid.NewGuid(), EntityId = identity, Scope = scope,
            Reference = version.Reference, ExpectedRevision = 0
        }, token);
        Assert.True(assigned.Success, assigned.ErrorMessage);
        var runId = Guid.NewGuid();
        var applied = await api.ApplyStartupAsync(new()
        {
            CommandId = runId, RunId = runId
        }, token);
        Assert.True(applied.Success, applied.ErrorMessage);
        var run = await api.StartupRunAsync(runId, token);
        Assert.True(run.Success, run.ErrorMessage);
        Assert.Contains(run.Value!.Scopes, candidate =>
            candidate.AssignmentId == identity.AssignmentId && candidate.Enabled);
        Assert.Contains(run.Value.Versions, candidate => candidate.Reference == version.Reference);
        Assert.Empty(run.Value.Plan.Steps);
        var disabled = await api.DisableAssignmentAsync(new()
        {
            CommandId = Guid.NewGuid(), EntityId = identity, Scope = scope,
            Reference = version.Reference, ExpectedRevision = 1
        }, token);
        Assert.True(disabled.Success, disabled.ErrorMessage);
    }

    [Fact]
    public async Task Real_actor_routes_complete_parameter_lifecycle_and_keep_frozen_startup_versions()
    {
        var api = new ParameterSetsApi(infrastructure.ActorProducer); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45)); var token = deadline.Token;
        var id = Guid.NewGuid(); var draft = await api.PreviewAsync(id, token); Assert.True(draft.Success, draft.ErrorMessage);
        var create = new CreateParameterSetCommand { CommandId = Guid.NewGuid(), EntityId = new(id), Name = "Runtime acceptance", SchemaVersion = ParameterSchemaRegistry.CurrentRegimeSchemaVersion, PayloadJson = draft.Value! };
        var created = await api.CreateAsync(create, token); Assert.True(created.Success, created.ErrorMessage);
        var repeated = await api.CreateAsync(create, token); Assert.True(repeated.Success, repeated.ErrorMessage);
        var saved = await api.StateAsync(id, token); Assert.True(saved.Success, saved.ErrorMessage); Assert.Equal(1, saved.Value!.Revision);
        var published = await api.PublishAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), Version = 1, ExpectedRevision = 1 }, token); Assert.True(published.Success, published.ErrorMessage);
        var version = (await api.StateAsync(id, token)).Value!.Versions.Single();
        var scope = WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id, TimeFrameType.Daily);
        var assignmentBefore = await api.AssignmentAsync(IntrinsicTimeStrategyWorkflowDefinition.Id, (int)TimeFrameType.Daily, token);
        var assignmentRevision = assignmentBefore.Value?.Revision ?? 0;
        var assign = await api.AssignAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(WorkflowParameterScopeModel.AssignmentId(scope)), Scope = scope, Reference = version.Reference, ExpectedRevision = assignmentRevision }, token); Assert.True(assign.Success, assign.ErrorMessage);
        var runId = Guid.NewGuid(); var apply = await api.ApplyStartupAsync(new() { CommandId = runId, RunId = runId }, token); Assert.True(apply.Success, apply.ErrorMessage);
        var selectedRun = await api.StartupRunAsync(runId, token); Assert.True(selectedRun.Success, selectedRun.ErrorMessage); var run = selectedRun.Value!;
        var report = new ParameterSignalStartupReport(runId, run.Plan.Fingerprint, new DateOnly(2026, 9, 10), "ES-PARAMETER-TEST", DateTime.UtcNow,
         run.Plan.Steps.Select(x => new ParameterSignalPreparationOutcome(x.Key, x.Prepare ? ParameterSignalPreparationStatus.ExistingRoute : ParameterSignalPreparationStatus.NotRequested, "Acceptance fixture; no live producers started.")).ToArray());
        var record = new RecordSignalStartupReportCommand { CommandId = Guid.NewGuid(), RunId = runId, Report = report };
        var recorded = await api.RecordStartupReportAsync(record, token); Assert.True(recorded.Success, recorded.ErrorMessage);
        var reportRetry = await api.RecordStartupReportAsync(record, token); Assert.True(reportRetry.Success, reportRetry.ErrorMessage);
        var readReport = await api.StartupReportAsync(runId, token); Assert.True(readReport.Success, readReport.ErrorMessage); Assert.Equal(report.Outcomes.Length, readReport.Value!.Outcomes.Length);
        var monitor = await api.SignalMonitoringAsync(runId, token); Assert.True(monitor.Success, monitor.ErrorMessage); Assert.NotEmpty(monitor.Value!.Rows);
        Assert.All(monitor.Value.Rows, x => Assert.Equal(TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery.RegimeDiscoverySignalAvailability.Missing, x.Observation.Availability));
        var inUse = await api.RetireAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), Version = 1, ExpectedRevision = 2 }, token); Assert.False(inUse.Success); Assert.Contains("PARAM.VERSION_IN_USE", inUse.ErrorMessage);
        var assignment = await api.AssignmentAsync(IntrinsicTimeStrategyWorkflowDefinition.Id, (int)TimeFrameType.Daily, token);
        var disabled = await api.DisableAssignmentAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(WorkflowParameterScopeModel.AssignmentId(scope)), Scope = scope, Reference = assignment.Value!.Assignment!.Reference, ExpectedRevision = assignment.Value.Revision }, token); Assert.True(disabled.Success, disabled.ErrorMessage);
        var retired = await api.RetireAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(id), Version = 1, ExpectedRevision = 2 }, token); Assert.True(retired.Success, retired.ErrorMessage);
        var retained = (await api.StartupRunsAsync(token)).Value!.Single(x => x.RunId == runId).Versions.Single(); Assert.Equal(ParameterVersionStatus.Published, retained.Status);
    }
}

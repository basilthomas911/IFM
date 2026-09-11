using System.Text.Json;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterAssignmentTests
{
    static ParameterAssignmentScope Scope(TimeFrameType horizon = TimeFrameType.Daily) => WorkflowParameterScopeModel.Create(IntrinsicTimeStrategyWorkflowDefinition.Id,horizon);
    static ParameterSetVersion Version()
    {
        var id=Guid.NewGuid(); var payload=new RegimeDiscoveryParameterModel().CreateDraftPayload(id);
        return new(new(id,1,RegimeDiscoveryParameterModel.ComponentCode,ParameterCanonicalPayloadModel.Hash(payload)),"Daily","",2,
            ParameterVersionStatus.Published,payload,DateTime.UtcNow,"test",DateTime.UtcNow);
    }
    [Fact] public void Scope_identity_is_stable_and_isolates_each_horizon()
    {
        WorkflowParameterScopeModel.AssignmentId(Scope()).Should().Be(WorkflowParameterScopeModel.AssignmentId(Scope()));
        new[]{TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly}.Select(x=>WorkflowParameterScopeModel.AssignmentId(Scope(x))).Distinct().Should().HaveCount(3);
        Action invalid=()=>WorkflowParameterScopeModel.Create("unknown",TimeFrameType.Daily);
        invalid.Should().Throw<ArgumentException>();
    }
    [Fact] public void Scope_tampering_and_wrong_horizon_are_rejected()
    {
        Action tamper=()=>WorkflowParameterScopeModel.Validate(Scope() with {ScopeSha256=new string('0',64)});
        tamper.Should().Throw<ArgumentException>();
        Action wrong=()=>ParameterAssignmentModel.Assign(Scope(TimeFrameType.Weekly),Version(),null,0,DateTime.UtcNow,"test");
        wrong.Should().Throw<ArgumentException>().WithMessage("PARAM.HORIZON_MISMATCH");
    }
    [Fact] public void Assignment_requires_published_exact_payload_and_expected_revision()
    {
        var version=Version();
        Action draft=()=>ParameterAssignmentModel.Assign(Scope(),version with {Status=ParameterVersionStatus.Draft},null,0,DateTime.UtcNow,"test");
        draft.Should().Throw<InvalidOperationException>().WithMessage("PARAM.VERSION_NOT_PUBLISHED");
        Action hash=()=>ParameterAssignmentModel.Assign(Scope(),version with {PayloadJson="{}"},null,0,DateTime.UtcNow,"test");
        hash.Should().Throw<ArgumentException>().WithMessage("PARAM.PAYLOAD_HASH_MISMATCH");
        Action conflict=()=>ParameterAssignmentModel.Assign(Scope(),version,null,1,DateTime.UtcNow,"test");
        conflict.Should().Throw<InvalidOperationException>().WithMessage("PARAM.REVISION_CONFLICT");
    }
    [Fact] public void Pending_change_cannot_replace_an_applied_startup_assignment()
    {
        var first=Version();var second=Version();var scope=Scope();var now=DateTime.UtcNow;
        var assigned=ParameterAssignmentModel.Assign(scope,first,null,0,now,"test");
        assigned=MessagePackSerializer.Deserialize<ParameterAssignmentRevision>(MessagePackSerializer.Serialize(assigned));
        var run=Guid.NewGuid();var source=new[]{assigned};
        var snapshot=new ParameterStartupSnapshotModel(run,source,new Dictionary<ParameterVersionRef,ParameterSetVersion>{{first.Reference,first}});
        source[0]=ParameterAssignmentModel.Assign(scope,second,assigned,1,now,"test");
        snapshot.Resolve(scope,run)!.Version.Should().Be(first);
        var next=new ParameterStartupSnapshotModel(Guid.NewGuid(),source,new Dictionary<ParameterVersionRef,ParameterSetVersion>{{second.Reference,second}});
        next.Fingerprint.Should().NotBe(snapshot.Fingerprint);
        next.Assignments.Single().Version.Should().Be(second);
        Action stale=()=>snapshot.Resolve(scope,Guid.NewGuid());stale.Should().Throw<InvalidOperationException>().WithMessage("PARAM.STARTUP_GENERATION_MISMATCH");
    }
    [Fact] public void Disabled_assignment_contributes_no_startup_demand_and_duplicate_scope_is_rejected()
    {
        var version=Version();var assignment=ParameterAssignmentModel.Assign(Scope(),version,null,0,DateTime.UtcNow,"test");
        var disabled=ParameterAssignmentModel.Disable(assignment,1,DateTime.UtcNow,"test");
        new ParameterStartupSnapshotModel(Guid.NewGuid(),[disabled],new Dictionary<ParameterVersionRef,ParameterSetVersion>()).Assignments.Should().BeEmpty();
        Action duplicate=()=>new ParameterStartupSnapshotModel(Guid.NewGuid(),[assignment,assignment],new Dictionary<ParameterVersionRef,ParameterSetVersion>{{version.Reference,version}});
        duplicate.Should().Throw<ArgumentException>().WithMessage("PARAM.ASSIGNMENT_ID_INVALID");
    }
}

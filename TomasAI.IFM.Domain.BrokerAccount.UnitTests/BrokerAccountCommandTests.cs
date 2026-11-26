using TomasAI.IFM.Domain.BrokerAccount.Command;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using AppBrokerEnvironment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment;
using Xunit;

namespace TomasAI.IFM.Domain.BrokerAccount.UnitTests;

public sealed class BrokerAccountCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Complete_snapshot_stays_closed_until_exact_manifest_is_explicitly_accepted()
    {
        var state = new BrokerAccountCommandState();
        var snapshot = Snapshot();
        Assert.True(snapshot.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Closed, state.BrokerAccountDefinition!.Gate);
        state.AcceptChanges();
        var evidence = Evidence();
        Assert.True(evidence.Execute(state).Success);
        Assert.Equal(BrokerAccountQualificationStatus.ReviewPending, state.BrokerAccountDefinition!.QualificationStatus);
        state.AcceptChanges();
        var mismatch = Accept() with { ManifestHash = "different" };
        Assert.False(mismatch.Execute(state).Success);
        Assert.Empty(state.Events);

        Assert.True(Accept().Execute(state).Success);
        Assert.Equal(BrokerAccountQualificationStatus.Accepted, state.BrokerAccountDefinition!.QualificationStatus);
        Assert.Equal(BrokerAccountOperationalGate.Open, state.BrokerAccountDefinition.Gate);
    }

    [Fact]
    public void Manual_hold_and_revocation_close_the_gate_and_release_rechecks_qualification()
    {
        var state = AcceptedState();
        state.AcceptChanges();
        Assert.True(new SetManualTradingHoldCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = Id(),
            Subject = Subject(SetManualTradingHoldCommand.Verb),
            Reason = "Operator investigation",
            EffectiveAtUtc = Now.AddMinutes(1)
        }.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Closed, state.BrokerAccountDefinition!.Gate);
        state.AcceptChanges();
        Assert.True(new ReleaseManualTradingHoldCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = Id(),
            Subject = Subject(ReleaseManualTradingHoldCommand.Verb),
            Reason = "Investigation complete",
            EffectiveAtUtc = Now.AddMinutes(2)
        }.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Open, state.BrokerAccountDefinition!.Gate);
        state.AcceptChanges();
        Assert.True(new RevokeAccountQualificationCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = Id(),
            Subject = Subject(RevokeAccountQualificationCommand.Verb),
            Reason = "Scenario version changed",
            AuthorizedBy = "operator",
            RevokedAtUtc = Now.AddMinutes(3)
        }.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Closed, state.BrokerAccountDefinition!.Gate);
        Assert.Equal(BrokerAccountQualificationStatus.Revoked, state.BrokerAccountDefinition.QualificationStatus);
    }

    [Fact]
    public void Stale_account_generation_is_a_successful_noop()
    {
        var state = new BrokerAccountCommandState();
        Assert.True(Snapshot(2).Execute(state).Success);
        state.AcceptChanges();
        Assert.True(Snapshot(1).Execute(state).Success);
        Assert.Empty(state.Events);
        Assert.Equal(2, state.BrokerAccountDefinition!.Snapshot!.Generation);
    }

    [Fact]
    public void Older_same_generation_snapshot_cannot_reopen_a_newer_closed_gate()
    {
        var state=AcceptedState();state.AcceptChanges();
        var latest=Snapshot();latest=latest with{Snapshot=latest.Snapshot with{AsOfUtc=Now.AddMinutes(2),NewRiskAllowed=false}};
        Assert.True(latest.Execute(state).Success);Assert.Equal(BrokerAccountOperationalGate.Closed,state.BrokerAccountDefinition!.Gate);state.AcceptChanges();
        var delayed=Snapshot();delayed=delayed with{Snapshot=delayed.Snapshot with{AsOfUtc=Now.AddMinutes(1),NewRiskAllowed=true}};
        Assert.True(delayed.Execute(state).Success);
        Assert.Equal(Now.AddMinutes(2),state.BrokerAccountDefinition!.Snapshot!.AsOfUtc);
        Assert.Equal(BrokerAccountOperationalGate.Closed,state.BrokerAccountDefinition.Gate);Assert.Empty(state.Events);
    }

    [Fact]
    public void Rejected_change_does_not_mutate_account_or_append_events()
    {
        var state = AcceptedState();
        state.AcceptChanges();
        var account = state.BrokerAccountDefinition;
        var result = new SetManualTradingHoldCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new BrokerAccountId("OTHER"),
            Subject = Subject(SetManualTradingHoldCommand.Verb), Reason = "Wrong account", EffectiveAtUtc = Now
        }.Execute(state);
        Assert.False(result.Success);
        Assert.Same(account, state.BrokerAccountDefinition);
        Assert.Empty(state.Events);
    }

    [Fact]
    public void Duplicate_snapshot_preserves_revision_and_replay_restores_account()
    {
        var restored = new BrokerAccountCommandState();
        var original = new BrokerAccountCommandState();
        Snapshot().Execute(original);
        Evidence().Execute(original);
        Accept().Execute(original);
        foreach (var accountEvent in original.Events) restored.Apply(accountEvent, false);
        Assert.Equal(original.BrokerAccountDefinition, restored.BrokerAccountDefinition);
        var account = original.BrokerAccountDefinition;
        original.AcceptChanges();
        Assert.True(Snapshot().Execute(original).Success);
        Assert.Same(account, original.BrokerAccountDefinition);
        Assert.Empty(original.Events);
    }

    [Fact]
    public void Moved_contract_resolves_from_its_original_assembly_qualified_name()
    {
        Assert.Equal("TomasAI.IFM.Application.Contracts.BrokerAccount", typeof(BrokerAccountChangedEvent).Assembly.GetName().Name);
        Assert.Equal(typeof(BrokerAccountChangedEvent), Type.GetType(
            "TomasAI.IFM.Domain.BrokerAccount.Contracts.BrokerAccountChangedEvent, TomasAI.IFM.Domain.BrokerAccount", throwOnError: true));
    }
    [Fact]
    public void Broker_revision_orders_same_time_evidence_and_older_revision_cannot_reopen_risk()
    {
        var state=AcceptedState();state.AcceptChanges();
        var latest=Snapshot() with{Snapshot=Snapshot().Snapshot with{SourceSequence=10,NewRiskAllowed=false}};
        Assert.True(latest.Execute(state).Success);state.AcceptChanges();
        var older=Snapshot() with{Snapshot=latest.Snapshot with{SourceSequence=9,AsOfUtc=Now.AddMinutes(10),NewRiskAllowed=true}};
        Assert.True(older.Execute(state).Success);Assert.Empty(state.Events);Assert.Equal(10,state.BrokerAccountDefinition!.Snapshot!.SourceSequence);
        var current=Snapshot() with{Snapshot=latest.Snapshot with{SourceSequence=11,NewRiskAllowed=true}};
        Assert.True(current.Execute(state).Success);Assert.Equal(BrokerAccountOperationalGate.Open,state.BrokerAccountDefinition.Gate);
    }

    [Fact]
    public void Equal_authority_conflict_fails_closed_preserving_prior_money_and_manual_holds()
    {
        var state=AcceptedState();state.AcceptChanges();
        var conflict=Snapshot() with{Snapshot=Snapshot().Snapshot with{CashBalance=999,AvailableFunds=999}};
        Assert.True(conflict.Execute(state).Success);Assert.Equal(100000,state.BrokerAccountDefinition!.Snapshot!.CashBalance);
        Assert.False(state.BrokerAccountDefinition.Snapshot.NewRiskAllowed);Assert.Equal(BrokerAccountOperationalGate.Closed,state.BrokerAccountDefinition.Gate);state.AcceptChanges();
        Assert.True(new SetManualTradingHoldCommand{CommandId=Guid.NewGuid(),EntityId=Id(),Subject=Subject(SetManualTradingHoldCommand.Verb),Reason="Keep manual hold",EffectiveAtUtc=Now}.Execute(state).Success);state.AcceptChanges();
        var fresh=Snapshot() with{Snapshot=Snapshot().Snapshot with{AsOfUtc=Now.AddMinutes(1),SourceSequence=1}};
        Assert.True(fresh.Execute(state).Success);Assert.True(state.BrokerAccountDefinition.ManualHold);Assert.Equal("Keep manual hold",state.BrokerAccountDefinition.Reason);
        Assert.Equal(BrokerAccountOperationalGate.Closed,state.BrokerAccountDefinition.Gate);
    }

    [Fact]
    public void Same_broker_revision_with_only_clock_refresh_is_a_noop()
    {
        var state=AcceptedState();state.AcceptChanges();var initial=Snapshot() with{Snapshot=Snapshot().Snapshot with{SourceSequence=5,AsOfUtc=Now.AddMinutes(1)}};
        Assert.True(initial.Execute(state).Success);state.AcceptChanges();
        Assert.True((initial with{CommandId=Guid.NewGuid(),Snapshot=initial.Snapshot with{AsOfUtc=Now.AddMinutes(2)}}).Execute(state).Success);
        Assert.Empty(state.Events);Assert.Equal(BrokerAccountOperationalGate.Open,state.BrokerAccountDefinition!.Gate);
    }

    private static BrokerAccountCommandState AcceptedState()
    {
        var state = new BrokerAccountCommandState();
        Snapshot().Execute(state);
        state.AcceptChanges();
        Evidence().Execute(state);
        state.AcceptChanges();
        Accept().Execute(state);
        return state;
    }

    private static RecordBrokerAccountSnapshotCommand Snapshot(long generation = 1) => new()
    {
        CommandId = Guid.NewGuid(),
        EntityId = Id(),
        Subject = Subject(RecordBrokerAccountSnapshotCommand.Verb),
        Environment = AppBrokerEnvironment.Emulator,
        Snapshot = new BrokerAccountSnapshotEvidence
        {
            AccountAlias = "EMU",
            Currency = "USD",
            CashBalance = 100_000m,
            AvailableFunds = 90_000m,
            Complete = true,
            NewRiskAllowed = true,
            Generation = generation,
            AsOfUtc = Now
        }
    };

    private static SubmitAccountQualificationEvidenceCommand Evidence() => new()
    {
        CommandId = Guid.NewGuid(),
        EntityId = Id(),
        Subject = Subject(SubmitAccountQualificationEvidenceCommand.Verb),
        ManifestHash = "manifest-v1",
        EvidenceReference = "artifact://emulator/v1",
        SubmittedAtUtc = Now
    };

    private static AcceptAccountQualificationCommand Accept() => new()
    {
        CommandId = Guid.NewGuid(),
        EntityId = Id(),
        Subject = Subject(AcceptAccountQualificationCommand.Verb),
        ApprovalId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
        ManifestHash = "manifest-v1",
        AuthorizedBy = "operator",
        ReviewedAtUtc = Now.AddSeconds(1)
    };

    private static BrokerAccountId Id() => new("EMU");

    private static ActorSubject Subject(string verb) =>
        new(ActorType.Command, BrokerAccountActorNames.Command, verb, Id().Format());
}

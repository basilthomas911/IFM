using TomasAI.IFM.Domain.BrokerAccount.Command;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using AppBrokerEnvironment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment;
using Xunit;

namespace TomasAI.IFM.Application.TradeBroker.UnitTests;

public sealed class BrokerAccountCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Complete_snapshot_stays_closed_until_exact_manifest_is_explicitly_accepted()
    {
        var state = new BrokerAccountCommandState();
        var snapshot = Snapshot();
        Assert.True(snapshot.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Closed, state.Current!.Gate);
        state.AcceptChanges();
        var evidence = Evidence();
        Assert.True(evidence.Execute(state).Success);
        Assert.Equal(BrokerAccountQualificationStatus.ReviewPending, state.Current!.QualificationStatus);
        state.AcceptChanges();
        var mismatch = Accept() with { ManifestHash = "different" };
        Assert.False(mismatch.Execute(state).Success);
        Assert.Empty(state.Events);

        Assert.True(Accept().Execute(state).Success);
        Assert.Equal(BrokerAccountQualificationStatus.Accepted, state.Current!.QualificationStatus);
        Assert.Equal(BrokerAccountOperationalGate.Open, state.Current.Gate);
    }

    [Fact]
    public void Manual_hold_and_revocation_close_the_gate_and_release_rechecks_qualification()
    {
        var state = AcceptedState();
        state.AcceptChanges();
        Assert.True(new SetManualTradingHoldCommand
        {
            CommandId = Guid.NewGuid(), EntityId = Id(), Subject = Subject(SetManualTradingHoldCommand.Verb),
            Reason = "Operator investigation", EffectiveAtUtc = Now.AddMinutes(1)
        }.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Closed, state.Current!.Gate);
        state.AcceptChanges();
        Assert.True(new ReleaseManualTradingHoldCommand
        {
            CommandId = Guid.NewGuid(), EntityId = Id(), Subject = Subject(ReleaseManualTradingHoldCommand.Verb),
            Reason = "Investigation complete", EffectiveAtUtc = Now.AddMinutes(2)
        }.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Open, state.Current!.Gate);
        state.AcceptChanges();
        Assert.True(new RevokeAccountQualificationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = Id(), Subject = Subject(RevokeAccountQualificationCommand.Verb),
            Reason = "Scenario version changed", AuthorizedBy = "operator", RevokedAtUtc = Now.AddMinutes(3)
        }.Execute(state).Success);
        Assert.Equal(BrokerAccountOperationalGate.Closed, state.Current!.Gate);
        Assert.Equal(BrokerAccountQualificationStatus.Revoked, state.Current.QualificationStatus);
    }

    [Fact]
    public void Stale_account_generation_is_a_successful_noop()
    {
        var state = new BrokerAccountCommandState();
        Assert.True(Snapshot(2).Execute(state).Success);
        state.AcceptChanges();
        Assert.True(Snapshot(1).Execute(state).Success);
        Assert.Empty(state.Events);
        Assert.Equal(2, state.Current!.Snapshot!.Generation);
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
        CommandId = Guid.NewGuid(), EntityId = Id(), Subject = Subject(RecordBrokerAccountSnapshotCommand.Verb),
        Environment = AppBrokerEnvironment.Emulator,
        Snapshot = new BrokerAccountSnapshotEvidence
        {
            AccountAlias = "EMU", Currency = "USD", CashBalance = 100_000m,
            AvailableFunds = 90_000m, Complete = true, NewRiskAllowed = true,
            Generation = generation, AsOfUtc = Now
        }
    };

    private static SubmitAccountQualificationEvidenceCommand Evidence() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id(), Subject = Subject(SubmitAccountQualificationEvidenceCommand.Verb),
        ManifestHash = "manifest-v1", EvidenceReference = "artifact://emulator/v1", SubmittedAtUtc = Now
    };

    private static AcceptAccountQualificationCommand Accept() => new()
    {
        CommandId = Guid.NewGuid(), EntityId = Id(), Subject = Subject(AcceptAccountQualificationCommand.Verb),
        ApprovalId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
        ManifestHash = "manifest-v1", AuthorizedBy = "operator", ReviewedAtUtc = Now.AddSeconds(1)
    };

    private static BrokerAccountId Id() => new("EMU");

    private static ActorSubject Subject(string verb) =>
        new(ActorType.Command, BrokerAccountActorNames.Command, verb, Id().Format());
}

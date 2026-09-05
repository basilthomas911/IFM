using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetDesiredSubscriptionRegistryTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 4);

    [Fact]
    public void Identical_canonical_contents_are_idempotent_despite_input_order()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var front = Registration("VX20260916", "VXU6", "XCBF.PITCH", "VX");
        var back = Registration("VX20261021", "VXV6", "XCBF.PITCH", "VX") with { OnTheRun = false };

        var first = registry.Set("XCBF.PITCH", ValueDate, [front, back]);
        var duplicate = registry.Set("XCBF.PITCH", ValueDate, [back, front]);

        duplicate.Should().BeSameAs(first);
        duplicate.Revision.Should().Be(1);
        duplicate.Fingerprint.Should().Be(first.Fingerprint);
        registry.Snapshot().Should().ContainSingle();
    }

    [Fact]
    public void Rollover_publishes_complete_new_manifest_and_preserves_old_snapshot()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var first = registry.Set("GLBX.MDP3", ValueDate, [Registration()]);
        var replacement = Registration("ES20261218", "ESZ6");

        var current = registry.Set("GLBX.MDP3", ValueDate, [replacement]);

        current.Revision.Should().Be(2);
        current.Fingerprint.Should().NotBe(first.Fingerprint);
        current.Contracts.Should().ContainSingle(item => item.DomainContractId == "ES20261218");
        first.Contracts.Should().ContainSingle(item => item.DomainContractId == "ES20260918");
        registry.IsCurrent(first).Should().BeFalse();
        registry.IsCurrent(current).Should().BeTrue();
    }

    [Fact]
    public void Same_revision_with_different_contents_cannot_be_acknowledged_as_current()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var current = registry.Set("GLBX.MDP3", ValueDate, [Registration()]);
        var forged = Manifest(current.Revision,
            Registration() with { ProviderContractName = "ES.WRONG" });
        var invoked = false;

        registry.TryWithCurrent(forged, () => invoked = true).Should().BeFalse();

        invoked.Should().BeFalse();
        registry.IsCurrent(forged).Should().BeFalse();
        registry.TryWithCurrent(current, () => invoked = true).Should().BeTrue();
        invoked.Should().BeTrue();
    }

    [Fact]
    public void Advancing_value_date_retains_one_manifest_and_rejects_old_date_resurrection()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var first = registry.Set("GLBX.MDP3", ValueDate, [Registration()]);

        var next = registry.Set("GLBX.MDP3", ValueDate.AddDays(1), [Registration()]);
        var staleWrite = () => registry.Set("GLBX.MDP3", ValueDate, [Registration()]);

        next.Revision.Should().Be(first.Revision + 1);
        registry.TryGet("GLBX.MDP3", ValueDate, out _).Should().BeFalse();
        registry.TryGet("GLBX.MDP3", ValueDate.AddDays(1), out var snapshot).Should().BeTrue();
        snapshot.Should().BeSameAs(next);
        staleWrite.Should().Throw<InvalidOperationException>();
        registry.Snapshot().Should().ContainSingle();
    }

    [Fact]
    public void Dataset_revisions_are_independent()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var vx = registry.Set("XCBF.PITCH", ValueDate,
            [Registration("VX20260916", "VXU6", "XCBF.PITCH", "VX")]);
        registry.Set("GLBX.MDP3", ValueDate, [Registration()]);

        registry.Set("GLBX.MDP3", ValueDate, [Registration("ES20261218", "ESZ6")]);

        registry.IsCurrent(vx).Should().BeTrue();
        registry.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public void A_serialized_current_manifest_is_accepted_but_stale_revision_date_or_dataset_is_not()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var previous = registry.Set("GLBX.MDP3", ValueDate, [Registration()]);
        var current = registry.Set("GLBX.MDP3", ValueDate, [Registration("ES20261218", "ESZ6")]);
        var wireCopy = MessagePackSerializer.Deserialize<DatasetSubscriptionManifest>(
            MessagePackSerializer.Serialize(current));
        var wrongDate = new DatasetSubscriptionManifest(current.Dataset, ValueDate.AddDays(1),
            current.Revision, current.Contracts);
        var wrongDataset = new DatasetSubscriptionManifest("TEST.DATA", ValueDate, current.Revision,
            [DatasetSubscriptionContract.FromRegistration(Registration() with { Dataset = "TEST.DATA" })]);
        var admitted = 0;

        registry.TryWithCurrent(previous, () => admitted++).Should().BeFalse();
        registry.TryWithCurrent(wrongDate, () => admitted++).Should().BeFalse();
        registry.TryWithCurrent(wrongDataset, () => admitted++).Should().BeFalse();
        registry.TryWithCurrent(wireCopy, () => admitted++).Should().BeTrue();

        admitted.Should().Be(1);
        registry.TryGet("GLBX.MDP3", ValueDate, out var desired).Should().BeTrue();
        desired.Should().BeSameAs(current);
    }

    [Fact]
    public void Dataset_registry_capacity_is_bounded_without_evicting_desired_state()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        for (var index = 0; index < DatasetDesiredSubscriptionRegistry.MaximumDatasets; index++)
        {
            var dataset = $"TEST.{index}";
            registry.Set(dataset, ValueDate, [Registration() with { Dataset = dataset }]);
        }
        var overflow = () => registry.Set("TEST.OVERFLOW", ValueDate,
            [Registration() with { Dataset = "TEST.OVERFLOW" }]);

        overflow.Should().Throw<InvalidOperationException>();

        registry.Snapshot().Should().HaveCount(DatasetDesiredSubscriptionRegistry.MaximumDatasets);
        registry.TryGet("TEST.0", ValueDate, out _).Should().BeTrue();
    }

    [Fact]
    public void Mutating_input_collections_cannot_change_published_manifest()
    {
        var source = new[] { Registration() };
        var registry = new DatasetDesiredSubscriptionRegistry();
        var manifest = registry.Set("GLBX.MDP3", ValueDate, source);
        source[0] = Registration("ES20261218", "ESZ6");

        manifest.Contracts.Should().ContainSingle(item => item.DomainContractId == "ES20260918");
        var mutateWire = () => ((IList<DatasetSubscriptionContract>)manifest.Contracts)[0] =
            DatasetSubscriptionContract.FromRegistration(source[0]);
        var mutateRegistrations = () => ((IList<DatabentoContractRegistration>)manifest.GetRegistrations())[0] = source[0];

        mutateWire.Should().Throw<NotSupportedException>();
        mutateRegistrations.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Manifest_constructor_defensively_copies_wire_contract_collection()
    {
        var wire = new[] { DatasetSubscriptionContract.FromRegistration(Registration()) };
        var manifest = new DatasetSubscriptionManifest("GLBX.MDP3", ValueDate, 1, wire);
        var originalFingerprint = manifest.Fingerprint;

        wire[0] = wire[0] with { ProviderContractName = "CHANGED" };

        manifest.Contracts[0].ProviderContractName.Should().Be("ESU6");
        manifest.Fingerprint.Should().Be(originalFingerprint);
    }

    [Fact]
    public void MessagePack_round_trip_retains_resolved_mapping_roles_identity_and_fingerprint()
    {
        var original = Manifest(12, Registration());

        var bytes = MessagePackSerializer.Serialize(original);
        var restored = MessagePackSerializer.Deserialize<DatasetSubscriptionManifest>(bytes);

        restored.Validate().Should().BeSameAs(restored);
        restored.Fingerprint.Should().Be(original.Fingerprint);
        restored.GetRegistrations().Should().BeEquivalentTo(original.GetRegistrations());
        restored.Dataset.Should().Be(original.Dataset);
        restored.ValueDate.Should().Be(original.ValueDate);
        restored.Revision.Should().Be(original.Revision);
        bytes.Length.Should().BeLessThan(DatasetSubscriptionManifest.MaximumManifestBytes);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("domain")]
    [InlineData("root")]
    [InlineData("on-the-run")]
    [InlineData("rollover")]
    public void Every_resolved_mapping_field_is_part_of_revision_identity(string changedField)
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var original = Registration();
        var before = registry.Set("GLBX.MDP3", ValueDate, [original]);
        var changed = changedField switch
        {
            "provider" => original with { ProviderContractName = "ESZ6" },
            "domain" => original with { DomainContractId = "ES20261218" },
            "root" => original with { RootSymbol = "TEST" },
            "on-the-run" => original with { OnTheRun = false },
            "rollover" => original with { Rollover = false },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };

        var after = registry.Set("GLBX.MDP3", ValueDate, [changed]);

        after.Revision.Should().Be(before.Revision + 1);
        after.Fingerprint.Should().NotBe(before.Fingerprint);
    }

    [Fact]
    public void Invalid_update_does_not_replace_current_manifest_or_consume_revision()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var current = registry.Set("GLBX.MDP3", ValueDate, [Registration()]);
        var invalid = () => registry.Set("GLBX.MDP3", ValueDate,
            [Registration() with { Dataset = "XCBF.PITCH" }]);

        invalid.Should().Throw<ArgumentException>();

        registry.IsCurrent(current).Should().BeTrue();
        registry.Set("GLBX.MDP3", ValueDate, [Registration("ES20261218", "ESZ6")])
            .Revision.Should().Be(2);
    }

    [Fact]
    public void Duplicate_domain_and_provider_identities_are_rejected()
    {
        var duplicateDomain = () => Manifest(1,
            Registration(), Registration() with { ProviderContractName = "ESZ6" });
        var duplicateProvider = () => Manifest(1,
            Registration(), Registration() with { DomainContractId = "ES20261218" });

        duplicateDomain.Should().Throw<ArgumentException>();
        duplicateProvider.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Stage_4_option_ownership_cannot_enter_stage_3_futures_manifest()
    {
        var act = () => Manifest(1, Registration() with { AssetTypeId = AssetTypeId.FuturesOption });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Empty_or_unbounded_manifest_is_rejected()
    {
        var empty = () => Manifest(1);
        var oversized = () => Manifest(1, Enumerable.Range(0, DatasetSubscriptionManifest.MaximumContracts + 1)
            .Select(index => Registration($"ES{index}", $"ES{index}")).ToArray());

        empty.Should().Throw<ArgumentException>();
        oversized.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_identity_invalid_date_revision_or_unbounded_identifier_is_rejected()
    {
        var invalidDate = () => new DatasetSubscriptionManifest("GLBX.MDP3", default, 1,
            [DatasetSubscriptionContract.FromRegistration(Registration())]);
        var invalidRevision = () => Manifest(0, Registration());
        var missingRoot = () => Manifest(1, Registration() with { RootSymbol = null });
        var oversizedIdentifier = () => Manifest(1,
            Registration() with { ProviderContractName = new string('X', 1025) });

        invalidDate.Should().Throw<ArgumentException>();
        invalidRevision.Should().Throw<ArgumentException>();
        missingRoot.Should().Throw<ArgumentException>();
        oversizedIdentifier.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Concurrent_identical_changes_publish_one_revision()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        registry.Set("GLBX.MDP3", ValueDate, [Registration()]);

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            registry.Set("GLBX.MDP3", ValueDate, [Registration("ES20261218", "ESZ6")]))));

        results.Should().OnlyContain(item => item.Revision == 2);
        results.Should().OnlyContain(item => ReferenceEquals(item, results[0]));
    }

    static DatasetSubscriptionManifest Manifest(long revision, params DatabentoContractRegistration[] contracts) =>
        new("GLBX.MDP3", ValueDate, revision,
            contracts.Select(DatasetSubscriptionContract.FromRegistration).ToArray());

    static DatabentoContractRegistration Registration(
        string domain = "ES20260918", string provider = "ESU6",
        string dataset = "GLBX.MDP3", string root = "ES") => new()
        {
            DomainContractId = domain,
            ProviderContractName = provider,
            AssetTypeId = AssetTypeId.Futures,
            Dataset = dataset,
            RootSymbol = root,
            OnTheRun = true,
            Rollover = true
        };
}

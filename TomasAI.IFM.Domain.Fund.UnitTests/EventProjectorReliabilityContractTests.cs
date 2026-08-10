using FluentAssertions;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class EventProjectorReliabilityContractTests
{
    [Fact]
    public void Effect_identity_is_stable_across_retries_and_distinct_per_effect()
    {
        var firstAttempt = new EventProjectorEffectIdentity(
            "FundEventProjector",
            42,
            EventProjectorEffectKind.TargetProjection);
        var retry = new EventProjectorEffectIdentity(
            "FundEventProjector",
            42,
            EventProjectorEffectKind.TargetProjection);
        var completion = new EventProjectorEffectIdentity(
            "FundEventProjector",
            42,
            EventProjectorEffectKind.CompletedPublication);

        retry.MessageId.Should().Be(firstAttempt.MessageId);
        completion.MessageId.Should().NotBe(firstAttempt.MessageId);
        firstAttempt.MessageId.Should().StartWith("ifm-projector-").And.HaveLength(78);
    }

    [Fact]
    public void Effect_identity_rejects_non_persisted_or_ambiguous_effects()
    {
        var missingProjector = () => new EventProjectorEffectIdentity(" ", 1, EventProjectorEffectKind.TargetProjection);
        var missingEvent = () => new EventProjectorEffectIdentity("FundEventProjector", 0, EventProjectorEffectKind.TargetProjection);
        var missingEffect = () => new EventProjectorEffectIdentity("FundEventProjector", 1, EventProjectorEffectKind.None);

        missingProjector.Should().Throw<ArgumentException>();
        missingEvent.Should().Throw<ArgumentOutOfRangeException>();
        missingEffect.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Reliability_options_are_bounded_and_default_to_valid_values()
    {
        new EventProjectorReliabilityOptions().Validate().Should().NotBeNull();

        var invalidBatch = new EventProjectorReliabilityOptions { RecoveryBatchSize = 0 };
        var invalidConcurrency = new EventProjectorReliabilityOptions { RecoveryStreamConcurrency = 33 };
        var invalidLease = new EventProjectorReliabilityOptions { ClaimLeaseDuration = TimeSpan.Zero };

        invalidBatch.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
        invalidConcurrency.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
        invalidLease.Invoking(options => options.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Descriptor_requires_an_explicit_idempotency_strategy()
    {
        var create = () => new EventProjectionDescriptor(
            typeof(FundCreatedEvent),
            EventProjectionIdempotencyStrategy.Unspecified,
            static (_, _) => ValueTask.FromResult(new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied)),
            static _ => null,
            static (_, _) => null);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Execution_context_rejects_an_effect_identity_from_another_event()
    {
        var mismatchedEffect = new EventProjectorEffectIdentity(
            "FundEventProjector",
            43,
            EventProjectorEffectKind.TargetProjection);

        var create = () => new ProjectionExecutionContext(
            "FundEventProjector",
            42,
            7,
            mismatchedEffect,
            Guid.NewGuid(),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            CancellationToken.None);

        create.Should().Throw<ArgumentException>();
    }
}

using System.Numerics;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Validated process, actor-type, and mailbox capacity configuration.</summary>
public sealed class ActorAdmissionOptions
{
    public const string SectionName = "ActorRuntime:Admission";
    public const int ExistingMailboxMessageLimit = 8192;
    public const int ExistingRetainedIdleMailboxesPerActor = 1024;

    public ActorAdmissionMode Mode { get; set; } = ActorAdmissionMode.Disabled;
    public ActorMailboxImplementation MailboxImplementation { get; set; } = ActorMailboxImplementation.Channel;
    public long GlobalMessageLimit { get; set; }
    public long GlobalByteLimit { get; set; }
    public int MaximumPayloadBytes { get; set; }
    public long DefaultActorTypeMessageLimit { get; set; }
    public long DefaultActorTypeByteLimit { get; set; }
    public Dictionary<ActorType, ActorTypeAdmissionOptions> ActorTypes { get; set; } = [];
    public int DefaultMailboxMessageLimit { get; set; } = ExistingMailboxMessageLimit;
    public int RetainedIdleMailboxesPerActor { get; set; } = ExistingRetainedIdleMailboxesPerActor;
    public int JetStreamNakDelayMilliseconds { get; set; } = 250;
    public int OverloadErrorCode { get; set; } = -429;

    public void Validate()
    {
        ActorTypes ??= [];
        if (!Enum.IsDefined(Mode))
            throw new InvalidOperationException($"Unknown actor admission mode '{Mode}'.");
        if (!Enum.IsDefined(MailboxImplementation))
            throw new InvalidOperationException($"Unknown actor mailbox implementation '{MailboxImplementation}'.");
        ValidateNonNegative(GlobalMessageLimit, nameof(GlobalMessageLimit));
        ValidateNonNegative(GlobalByteLimit, nameof(GlobalByteLimit));
        ValidateNonNegative(MaximumPayloadBytes, nameof(MaximumPayloadBytes));
        ValidateNonNegative(DefaultActorTypeMessageLimit, nameof(DefaultActorTypeMessageLimit));
        ValidateNonNegative(DefaultActorTypeByteLimit, nameof(DefaultActorTypeByteLimit));
        if (DefaultMailboxMessageLimit <= 0)
            throw new InvalidOperationException($"{nameof(DefaultMailboxMessageLimit)} must be greater than zero.");
        if (MailboxImplementation is ActorMailboxImplementation.MpscRing or ActorMailboxImplementation.SpscRing
            && !BitOperations.IsPow2(DefaultMailboxMessageLimit))
        {
            throw new InvalidOperationException(
                $"{nameof(DefaultMailboxMessageLimit)} must be a power of two for {MailboxImplementation}.");
        }
        if (RetainedIdleMailboxesPerActor < 0)
            throw new InvalidOperationException($"{nameof(RetainedIdleMailboxesPerActor)} cannot be negative.");
        if (JetStreamNakDelayMilliseconds < 0)
            throw new InvalidOperationException($"{nameof(JetStreamNakDelayMilliseconds)} cannot be negative.");

        foreach (var (actorType, limits) in ActorTypes)
        {
            if (!Enum.IsDefined(actorType))
                throw new InvalidOperationException($"Unknown actor type '{actorType}' in admission options.");
            ArgumentNullException.ThrowIfNull(limits);
            ValidateNonNegative(limits.MessageLimit, $"ActorTypes:{actorType}:MessageLimit");
            ValidateNonNegative(limits.ByteLimit, $"ActorTypes:{actorType}:ByteLimit");
            ValidateChildLimit(limits.MessageLimit, GlobalMessageLimit, $"ActorTypes:{actorType}:MessageLimit");
            ValidateChildLimit(limits.ByteLimit, GlobalByteLimit, $"ActorTypes:{actorType}:ByteLimit");
        }

        ValidateChildLimit(DefaultActorTypeMessageLimit, GlobalMessageLimit, nameof(DefaultActorTypeMessageLimit));
        ValidateChildLimit(DefaultActorTypeByteLimit, GlobalByteLimit, nameof(DefaultActorTypeByteLimit));
        ValidateChildLimit(DefaultMailboxMessageLimit, GlobalMessageLimit, nameof(DefaultMailboxMessageLimit));
        ValidateChildLimit(MaximumPayloadBytes, GlobalByteLimit, nameof(MaximumPayloadBytes));

        if (Mode == ActorAdmissionMode.Enforce)
            ValidateEnforcementConfiguration();
    }

    internal ActorTypeAdmissionOptions GetLimits(ActorType actorType)
        => ActorTypes.TryGetValue(actorType, out var limits)
            ? limits
            : new ActorTypeAdmissionOptions
            {
                MessageLimit = DefaultActorTypeMessageLimit,
                ByteLimit = DefaultActorTypeByteLimit
            };

    void ValidateEnforcementConfiguration()
    {
        if (GlobalMessageLimit <= 0 || GlobalByteLimit <= 0 || MaximumPayloadBytes <= 0)
        {
            throw new InvalidOperationException(
                "Enforced actor admission requires positive global message, global byte, and maximum payload limits.");
        }
        if (DefaultActorTypeMessageLimit <= 0 || DefaultActorTypeByteLimit <= 0)
        {
            throw new InvalidOperationException(
                "Enforced actor admission requires positive default actor-type message and byte limits.");
        }
        if (JetStreamNakDelayMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                $"Enforced actor admission requires a positive {nameof(JetStreamNakDelayMilliseconds)}.");
        }
        if (OverloadErrorCode == 0)
        {
            throw new InvalidOperationException(
                $"Enforced actor admission requires a non-zero {nameof(OverloadErrorCode)}.");
        }

        foreach (var actorType in new[]
                 {
                     ActorType.Command,
                     ActorType.Query,
                     ActorType.Event,
                     ActorType.Notify,
                     ActorType.Realtime
                 })
        {
            var limits = GetLimits(actorType);
            if (limits.MessageLimit <= 0 || limits.ByteLimit <= 0)
                throw new InvalidOperationException($"Enforced actor admission requires positive limits for {actorType}.");
        }
    }

    static void ValidateNonNegative(long value, string name)
    {
        if (value < 0)
            throw new InvalidOperationException($"{name} cannot be negative.");
    }

    static void ValidateChildLimit(long value, long parent, string name)
    {
        if (value > 0 && parent > 0 && value > parent)
            throw new InvalidOperationException($"{name} cannot exceed its process-wide limit.");
    }
}

public sealed class ActorTypeAdmissionOptions
{
    public long MessageLimit { get; set; }
    public long ByteLimit { get; set; }
}

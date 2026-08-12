using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Reserves process-wide and actor-type actor-backlog capacity without monitor locks.
/// Observe-only mode records the same decisions without rejecting messages.
/// </summary>
public sealed class ActorAdmissionController
{
    static readonly int ActorTypeCapacity = Enum.GetValues<ActorType>().Max(static value => (int)value) + 1;
    static readonly ActorAdmissionController DisabledController = new(new ActorAdmissionOptions());

    readonly ActorAdmissionMode _mode;
    readonly long _globalMessageLimit;
    readonly long _globalByteLimit;
    readonly int _maximumPayloadBytes;
    readonly long[] _typeMessageLimits;
    readonly long[] _typeByteLimits;
    readonly long[] _typeMessages;
    readonly long[] _typeBytes;
    long _messages;
    long _bytes;

    public ActorAdmissionController(ActorAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _mode = options.Mode;
        _globalMessageLimit = options.GlobalMessageLimit;
        _globalByteLimit = options.GlobalByteLimit;
        _maximumPayloadBytes = options.MaximumPayloadBytes;

        _typeMessageLimits = new long[ActorTypeCapacity];
        _typeByteLimits = new long[ActorTypeCapacity];
        _typeMessages = new long[ActorTypeCapacity];
        _typeBytes = new long[ActorTypeCapacity];
        foreach (var actorType in Enum.GetValues<ActorType>())
        {
            var limits = options.GetLimits(actorType);
            _typeMessageLimits[(int)actorType] = limits.MessageLimit;
            _typeByteLimits[(int)actorType] = limits.ByteLimit;
        }
    }

    internal static ActorAdmissionController Disabled => DisabledController;
    public ActorAdmissionMode Mode => _mode;
    public bool IsEnabled => _mode != ActorAdmissionMode.Disabled;
    internal long CurrentMessageCount => Volatile.Read(ref _messages);
    internal long CurrentByteCount => Volatile.Read(ref _bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ActorAdmissionResult TryReserve(
        IActorMessage message,
        ActorType actorType,
        out ActorAdmissionCharge charge)
    {
        charge = default;
        if (_mode == ActorAdmissionMode.Disabled)
            return ActorAdmissionResult.AcceptedResult;

        var payloadBytes = Math.Max(0, message.AdmissionSizeBytes);
        var actorTypeIndex = NormalizeActorType(actorType);
        if (_mode == ActorAdmissionMode.Enforce)
            return TryReserveEnforced(actorType, actorTypeIndex, payloadBytes, out charge);

        var messages = Interlocked.Increment(ref _messages);
        var bytes = Interlocked.Add(ref _bytes, payloadBytes);
        var typeMessages = Interlocked.Increment(ref _typeMessages[actorTypeIndex]);
        var typeBytes = Interlocked.Add(ref _typeBytes[actorTypeIndex], payloadBytes);

        ActorRuntimeMetrics.RecordAdmissionAccepted(actorType, payloadBytes);
        var reason = GetWouldRejectReason(
            actorTypeIndex,
            payloadBytes,
            messages,
            bytes,
            typeMessages,
            typeBytes);
        if (reason != ActorAdmissionReason.None)
            ActorRuntimeMetrics.RecordAdmissionWouldReject(actorType, reason);

        charge = new ActorAdmissionCharge(true, actorType, payloadBytes);
        return ActorAdmissionResult.AcceptedResult;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RecordQueueRejection(ActorType actorType, ActorAdmissionReason reason)
    {
        if (_mode == ActorAdmissionMode.ObserveOnly && reason == ActorAdmissionReason.MailboxLimit)
            ActorRuntimeMetrics.RecordAdmissionWouldReject(actorType, ActorAdmissionReason.MailboxLimit);
        else if (_mode == ActorAdmissionMode.Enforce)
            ActorRuntimeMetrics.RecordAdmissionRejected(actorType, reason);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release(ActorAdmissionCharge charge)
    {
        if (!charge.IsTracked)
            return;

        var actorTypeIndex = NormalizeActorType(charge.ActorType);
        Interlocked.Decrement(ref _messages);
        Interlocked.Add(ref _bytes, -charge.PayloadBytes);
        Interlocked.Decrement(ref _typeMessages[actorTypeIndex]);
        Interlocked.Add(ref _typeBytes[actorTypeIndex], -charge.PayloadBytes);
        ActorRuntimeMetrics.RecordAdmissionReleased(charge.ActorType, charge.PayloadBytes);
    }

    ActorAdmissionResult TryReserveEnforced(
        ActorType actorType,
        int actorTypeIndex,
        int payloadBytes,
        out ActorAdmissionCharge charge)
    {
        charge = default;
        if (payloadBytes > _maximumPayloadBytes)
            return Reject(actorType, ActorAdmissionReason.PayloadTooLarge);
        if (!TryAddWithinLimit(ref _messages, 1, _globalMessageLimit))
            return Reject(actorType, ActorAdmissionReason.GlobalMessageLimit);
        if (!TryAddWithinLimit(ref _bytes, payloadBytes, _globalByteLimit))
        {
            Interlocked.Decrement(ref _messages);
            return Reject(actorType, ActorAdmissionReason.GlobalByteLimit);
        }
        if (!TryAddWithinLimit(ref _typeMessages[actorTypeIndex], 1, _typeMessageLimits[actorTypeIndex]))
        {
            Interlocked.Add(ref _bytes, -payloadBytes);
            Interlocked.Decrement(ref _messages);
            return Reject(actorType, ActorAdmissionReason.ActorTypeMessageLimit);
        }
        if (!TryAddWithinLimit(ref _typeBytes[actorTypeIndex], payloadBytes, _typeByteLimits[actorTypeIndex]))
        {
            Interlocked.Decrement(ref _typeMessages[actorTypeIndex]);
            Interlocked.Add(ref _bytes, -payloadBytes);
            Interlocked.Decrement(ref _messages);
            return Reject(actorType, ActorAdmissionReason.ActorTypeByteLimit);
        }

        ActorRuntimeMetrics.RecordAdmissionAccepted(actorType, payloadBytes);
        charge = new ActorAdmissionCharge(true, actorType, payloadBytes);
        return ActorAdmissionResult.AcceptedResult;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryAddWithinLimit(ref long location, long amount, long limit)
    {
        while (true)
        {
            var current = Volatile.Read(ref location);
            if (current > limit - amount)
                return false;
            if (Interlocked.CompareExchange(ref location, current + amount, current) == current)
                return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ActorAdmissionResult Reject(ActorType actorType, ActorAdmissionReason reason)
    {
        ActorRuntimeMetrics.RecordAdmissionRejected(actorType, reason);
        return ActorAdmissionResult.Rejected(reason);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ActorAdmissionReason GetWouldRejectReason(
        int actorTypeIndex,
        int payloadBytes,
        long messages,
        long bytes,
        long typeMessages,
        long typeBytes)
    {
        if (_maximumPayloadBytes > 0 && payloadBytes > _maximumPayloadBytes)
            return ActorAdmissionReason.PayloadTooLarge;
        if (_globalMessageLimit > 0 && messages > _globalMessageLimit)
            return ActorAdmissionReason.GlobalMessageLimit;
        if (_globalByteLimit > 0 && bytes > _globalByteLimit)
            return ActorAdmissionReason.GlobalByteLimit;
        var typeMessageLimit = _typeMessageLimits[actorTypeIndex];
        if (typeMessageLimit > 0 && typeMessages > typeMessageLimit)
            return ActorAdmissionReason.ActorTypeMessageLimit;
        var typeByteLimit = _typeByteLimits[actorTypeIndex];
        return typeByteLimit > 0 && typeBytes > typeByteLimit
            ? ActorAdmissionReason.ActorTypeByteLimit
            : ActorAdmissionReason.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int NormalizeActorType(ActorType actorType)
        => actorType is ActorType.Command
            or ActorType.Event
            or ActorType.Query
            or ActorType.Notify
            or ActorType.Realtime
            ? (int)actorType
            : (int)ActorType.Unknown;
}

readonly record struct ActorAdmissionCharge(bool IsTracked, ActorType ActorType, int PayloadBytes);

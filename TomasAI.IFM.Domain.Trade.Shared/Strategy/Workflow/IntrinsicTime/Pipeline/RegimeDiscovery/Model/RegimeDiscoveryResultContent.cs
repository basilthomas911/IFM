using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Copies typed results and hashes their ordered fields without encoding an inner message payload.</summary>
/// <remarks>Fingerprint v1 uses length-prefixed UTF-8 strings, little-endian integers, and ordered collections.
/// Every keyed result field is included. Update the field-coverage test when evolving the result contract.</remarks>
public static class RegimeDiscoveryResultContent
{
    /// <summary>Computes the v1 content fingerprint and canonical content size without a serialized result buffer.</summary>
    /// <param name="result">The complete typed result to fingerprint.</param>
    /// <returns>The SHA-256 fingerprint and number of canonical bytes examined.</returns>
    public static (string Hash, int Size) Fingerprint(RegimeDiscoveryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        using var writer = new FingerprintWriter();
        writer.String("IFM.RegimeDiscoveryResult.Content.v1");
        Append(writer, result);
        return (writer.Finish(), writer.Size);
    }

    /// <summary>Fingerprints a decision by value, including ordered reasons and restrictions.</summary>
    /// <param name="decision">The decision to compare across typed and JSON storage representations.</param>
    /// <returns>A stable content digest independent of decimal scale.</returns>
    public static string DecisionFingerprint(RegimeDiscoveryDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        using var writer = new FingerprintWriter();
        writer.String("IFM.RegimeDiscoveryDecision.Content.v1");
        Append(writer, decision);
        return writer.Finish();
    }

    // Fingerprint fields: TrendRegimeResult: IsComplete, Direction, Strength, Phase, Score, Confidence, ConfidenceBand, TimeFrameAgreement, Evidence, Reasons
    static void Append(FingerprintWriter writer, TrendRegimeResult? value)
    {
        writer.Number(value is null ? 0 : 1);
        if (value is null) return;
        writer.Number(value.IsComplete ? 1 : 0);
        writer.Number((long)value.Direction);
        writer.Number((long)value.Strength);
        writer.Number((long)value.Phase);
        writer.String(value.Score.ToString("G29", CultureInfo.InvariantCulture));
        writer.String(value.Confidence.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number((long)value.ConfidenceBand);
        writer.String(value.TimeFrameAgreement.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number(value.Evidence?.Length ?? -1);
        if (value.Evidence is not null)
            foreach (var item in value.Evidence) { Append(writer, item); }
        writer.Number(value.Reasons?.Length ?? -1);
        if (value.Reasons is not null)
            foreach (var item in value.Reasons) { Append(writer, item); }
    }

    // Fingerprint fields: VolatilityRegimeResult: IsComplete, Level, Change, TermStructure, Score, Confidence, ConfidenceBand, NoNewTrade, Evidence, Reasons
    static void Append(FingerprintWriter writer, VolatilityRegimeResult? value)
    {
        writer.Number(value is null ? 0 : 1);
        if (value is null) return;
        writer.Number(value.IsComplete ? 1 : 0);
        writer.Number((long)value.Level);
        writer.Number((long)value.Change);
        writer.Number((long)value.TermStructure);
        writer.String(value.Score.ToString("G29", CultureInfo.InvariantCulture));
        writer.String(value.Confidence.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number((long)value.ConfidenceBand);
        writer.Number(value.NoNewTrade ? 1 : 0);
        writer.Number(value.Evidence?.Length ?? -1);
        if (value.Evidence is not null)
            foreach (var item in value.Evidence) { Append(writer, item); }
        writer.Number(value.Reasons?.Length ?? -1);
        if (value.Reasons is not null)
            foreach (var item in value.Reasons) { Append(writer, item); }
    }

    // Fingerprint fields: MarketStructureRegimeResult: IsComplete, Classification, Direction, Breakout, Score, Confidence, ConfidenceBand, Evidence, Reasons
    static void Append(FingerprintWriter writer, MarketStructureRegimeResult? value)
    {
        writer.Number(value is null ? 0 : 1);
        if (value is null) return;
        writer.Number(value.IsComplete ? 1 : 0);
        writer.Number((long)value.Classification);
        writer.Number((long)value.Direction);
        writer.Number((long)value.Breakout);
        writer.String(value.Score.ToString("G29", CultureInfo.InvariantCulture));
        writer.String(value.Confidence.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number((long)value.ConfidenceBand);
        writer.Number(value.Evidence?.Length ?? -1);
        if (value.Evidence is not null)
            foreach (var item in value.Evidence) { Append(writer, item); }
        writer.Number(value.Reasons?.Length ?? -1);
        if (value.Reasons is not null)
            foreach (var item in value.Reasons) { Append(writer, item); }
    }

    // Fingerprint fields: RegimeDiscoveryDecision: IsComplete, Direction, DirectionalScore, RiskAdjustedConviction, Confidence, ConfidenceBand, Quality, Restrictions, Reasons, TrendPhase, TrendStrength, TrendTimeFrameAgreement, VolatilityLevel, VolatilityChange, TermStructure, StructureClassification, Breakout
    static void Append(FingerprintWriter writer, RegimeDiscoveryDecision? value)
    {
        writer.Number(value is null ? 0 : 1);
        if (value is null) return;
        writer.Number(value.IsComplete ? 1 : 0);
        writer.Number((long)value.Direction);
        writer.String(value.DirectionalScore.ToString("G29", CultureInfo.InvariantCulture));
        writer.String(value.RiskAdjustedConviction.ToString("G29", CultureInfo.InvariantCulture));
        writer.String(value.Confidence.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number((long)value.ConfidenceBand);
        writer.Number((long)value.Quality);
        writer.Number(value.Restrictions?.Length ?? -1);
        if (value.Restrictions is not null)
            foreach (var item in value.Restrictions) { writer.Number((long)item); }
        writer.Number(value.Reasons?.Length ?? -1);
        if (value.Reasons is not null)
            foreach (var item in value.Reasons) { Append(writer, item); }
        writer.Number((long)value.TrendPhase);
        writer.Number((long)value.TrendStrength);
        writer.String(value.TrendTimeFrameAgreement.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number((long)value.VolatilityLevel);
        writer.Number((long)value.VolatilityChange);
        writer.Number((long)value.TermStructure);
        writer.Number((long)value.StructureClassification);
        writer.Number((long)value.Breakout);
    }

    // Fingerprint fields: RegimeDiscoveryResult: SchemaVersion, ResultId, WorkflowId, StrategyParameterSetId, StrategyParameterSetVersion, RegimeDiscoveryParameterSetId, RegimeDiscoveryParameterSetVersion, SignalSnapshotId, EntityId, TriggerEventId, MarketDataAsOfUtc, ProducedAtUtc, TargetHorizon, Trend, Volatility, MarketStructure, Decision, SupportingEvidence, OverallQuality, OverallConfidence, Reasons, SummaryText
    static void Append(FingerprintWriter writer, RegimeDiscoveryResult? value)
    {
        writer.Number(value is null ? 0 : 1);
        if (value is null) return;
        writer.Number((long)value.SchemaVersion);
        writer.Guid(value.ResultId);
        writer.Guid(value.WorkflowId.Value);
        writer.Guid(value.StrategyParameterSetId);
        writer.Number((long)value.StrategyParameterSetVersion);
        writer.Guid(value.RegimeDiscoveryParameterSetId);
        writer.Number((long)value.RegimeDiscoveryParameterSetVersion);
        writer.Guid(value.SignalSnapshotId);
        writer.String(value.EntityId.WorkflowDefinitionId);
        writer.Number(value.EntityId.ItiSignalEntityId is null ? 0 : 1);
        if (value.EntityId.ItiSignalEntityId is { } signal)
        {
            writer.String(signal.ContractId);
            writer.Number(signal.ValueDate.DayNumber);
            writer.Number((long)signal.TimePeriod);
        }
        writer.Guid(value.TriggerEventId);
        writer.Number(value.MarketDataAsOfUtc.Ticks);
        writer.Number(value.ProducedAtUtc.Ticks);
        writer.Number((long)value.TargetHorizon);
        Append(writer, value.Trend);
        Append(writer, value.Volatility);
        Append(writer, value.MarketStructure);
        Append(writer, value.Decision);
        writer.Number(value.SupportingEvidence?.Length ?? -1);
        if (value.SupportingEvidence is not null)
            foreach (var item in value.SupportingEvidence) { Append(writer, item); }
        writer.Number((long)value.OverallQuality);
        writer.String(value.OverallConfidence.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number(value.Reasons?.Length ?? -1);
        if (value.Reasons is not null)
            foreach (var item in value.Reasons) { Append(writer, item); }
        writer.String(value.SummaryText);
    }

    // Fingerprint fields: RegimeDiscoveryEvidence: Area, EvidenceId, SignalKind, TimeFrame, Value, Weight, FreshnessFactor, IsRequired, IsAvailable, MarketDataAsOfUtc, SignalIdentity
    static void Append(FingerprintWriter writer, RegimeDiscoveryEvidence? value)
    {
        writer.Number(value is null ? 0 : 1);
        if (value is null) return;
        writer.Number((long)value.Area);
        writer.String(value.EvidenceId);
        writer.Number((long)value.SignalKind);
        writer.Number((long)value.TimeFrame);
        writer.String(value.Value.ToString("G29", CultureInfo.InvariantCulture));
        writer.String(value.Weight.ToString("G29", CultureInfo.InvariantCulture));
        writer.String(value.FreshnessFactor.ToString("G29", CultureInfo.InvariantCulture));
        writer.Number(value.IsRequired ? 1 : 0);
        writer.Number(value.IsAvailable ? 1 : 0);
        writer.Number(value.MarketDataAsOfUtc.Ticks);
        writer.String(value.SignalIdentity);
    }

    // Fingerprint fields: RegimeDiscoveryReason: Code, Severity, Area, TimeFrame, SignalIdentity
    static void Append(FingerprintWriter writer, RegimeDiscoveryReason? value)
    {
        writer.Number(value is null ? 0 : 1);
        if (value is null) return;
        writer.String(value.Code);
        writer.Number((long)value.Severity);
        writer.Number((long)value.Area);
        writer.Number((long)value.TimeFrame);
        writer.String(value.SignalIdentity);
    }

    static TrendRegimeResult? Clone(TrendRegimeResult? value) => value is null ? null : value with
    {
        Evidence = value.Evidence?.Select(item => Clone(item)!).ToArray()!,
        Reasons = value.Reasons?.Select(item => Clone(item)!).ToArray()!,
    };

    static VolatilityRegimeResult? Clone(VolatilityRegimeResult? value) => value is null ? null : value with
    {
        Evidence = value.Evidence?.Select(item => Clone(item)!).ToArray()!,
        Reasons = value.Reasons?.Select(item => Clone(item)!).ToArray()!,
    };

    static MarketStructureRegimeResult? Clone(MarketStructureRegimeResult? value) => value is null ? null : value with
    {
        Evidence = value.Evidence?.Select(item => Clone(item)!).ToArray()!,
        Reasons = value.Reasons?.Select(item => Clone(item)!).ToArray()!,
    };

    /// <summary>Copies a decision and its nested collections without serialization.</summary>
    public static RegimeDiscoveryDecision? Clone(RegimeDiscoveryDecision? value) => value is null ? null : value with
    {
        Restrictions = value.Restrictions?.ToArray()!,
        Reasons = value.Reasons?.Select(item => Clone(item)!).ToArray()!,
    };

    /// <summary>Creates a snapshot with independent arrays throughout the typed result graph.</summary>
    /// <param name="value">The result to copy, or null.</param>
    /// <returns>A defensively copied result, or null.</returns>
    public static RegimeDiscoveryResult? Clone(RegimeDiscoveryResult? value) => value is null ? null : value with
    {
        Trend = Clone(value.Trend)!,
        Volatility = Clone(value.Volatility)!,
        MarketStructure = Clone(value.MarketStructure)!,
        Decision = Clone(value.Decision)!,
        SupportingEvidence = value.SupportingEvidence?.Select(item => Clone(item)!).ToArray()!,
        Reasons = value.Reasons?.Select(item => Clone(item)!).ToArray()!,
    };

    static RegimeDiscoveryEvidence? Clone(RegimeDiscoveryEvidence? value) => value is null ? null : value with
    {
    };

    static RegimeDiscoveryReason? Clone(RegimeDiscoveryReason? value) => value is null ? null : value with
    {
    };

    sealed class FingerprintWriter : IDisposable
    {
        readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public int Size { get; private set; }
        void Bytes(ReadOnlySpan<byte> bytes) { _hash.AppendData(bytes); Size = checked(Size + bytes.Length); }
        public void Number(long value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
            Bytes(bytes);
        }
        public void Guid(Guid value)
        {
            Span<byte> bytes = stackalloc byte[16];
            value.TryWriteBytes(bytes);
            Bytes(bytes);
        }
        public void String(string? value)
        {
            if (value is null) { Number(-1); return; }
            var length = Encoding.UTF8.GetByteCount(value);
            Number(length);
            byte[]? rented = null;
            Span<byte> bytes = length <= 256 ? stackalloc byte[length] : (rented = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);
            try { Encoding.UTF8.GetBytes(value, bytes); Bytes(bytes); }
            finally { if (rented is not null) ArrayPool<byte>.Shared.Return(rented); }
        }
        public string Finish() => Convert.ToHexString(_hash.GetHashAndReset());
        public void Dispose() => _hash.Dispose();
    }
}

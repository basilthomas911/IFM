using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseBackupLineage
{
    [Key(0)] public DatabaseBackupMode RequestedMode { get; init; }
    [Key(1)] public DatabaseBackupMode ResolvedMode { get; init; }
    [Key(2)] public DatabaseNativeBackupKind NativeKind { get; init; }
    [Key(3)] public DatabaseRestorePointId? BaseRestorePointId { get; init; }
    [Key(4)] public DatabaseRestorePointId? ParentRestorePointId { get; init; }
    [Key(5)] public int ChainDepth { get; init; }
    [Key(6)] public string NativeIdentity { get; init; } = string.Empty;

    public DatabaseBackupLineage NormalizeLegacyFull(DatabaseEngine engine = DatabaseEngine.None)
        => (ResolvedMode != DatabaseBackupMode.None) switch { true => this, false => this with
            {
                RequestedMode = RequestedMode == DatabaseBackupMode.None ? DatabaseBackupMode.Full : RequestedMode,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = NativeKind != DatabaseNativeBackupKind.None
                    ? NativeKind
                    : engine switch
                    {
                        DatabaseEngine.PostgreSql => DatabaseNativeBackupKind.PostgreSqlBase,
                        DatabaseEngine.ScyllaDb => DatabaseNativeBackupKind.ScyllaManagerSnapshot,
                        _ => DatabaseNativeBackupKind.None
                    },
                ChainDepth = 0
            } };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This validates persisted instance properties, not method arguments; the property name identifies the invalid stored field.")]
    public void Validate(bool resolvedRequired)
    {
        DatabaseBackupEnumValidation.RequireOptionalDefined(RequestedMode, nameof(RequestedMode));
        DatabaseBackupEnumValidation.RequireOptionalDefined(ResolvedMode, nameof(ResolvedMode));
        DatabaseBackupEnumValidation.RequireOptionalDefined(NativeKind, nameof(NativeKind));
        if (resolvedRequired && ResolvedMode == DatabaseBackupMode.None)
            throw new ArgumentException("A resolved backup mode is required.", nameof(ResolvedMode));
        if (resolvedRequired && NativeKind == DatabaseNativeBackupKind.None)
            throw new ArgumentException("A resolved native backup kind is required.", nameof(NativeKind));
        if (ChainDepth < 0) throw new ArgumentOutOfRangeException(nameof(ChainDepth));
        if (ResolvedMode == DatabaseBackupMode.Full
            && (ParentRestorePointId is not null || ChainDepth != 0))
            throw new ArgumentException("A full backup cannot have a parent or non-zero chain depth.");
        if (ResolvedMode == DatabaseBackupMode.Incremental
            && (BaseRestorePointId is null || ParentRestorePointId is null || ChainDepth <= 0))
            throw new ArgumentException("An incremental backup requires base, parent, and chain depth.");
        var incrementalNativeKind = NativeKind is DatabaseNativeBackupKind.PostgreSqlIncremental
            or DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot;
        if (ResolvedMode == DatabaseBackupMode.Full && incrementalNativeKind)
            throw new ArgumentException("A full backup cannot use an incremental native kind.");
        if (ResolvedMode == DatabaseBackupMode.Incremental && !incrementalNativeKind)
            throw new ArgumentException("An incremental backup requires an incremental native kind.");
        if (NativeIdentity is null || NativeIdentity.Any(char.IsControl)
            || NativeIdentity.Length > DatabaseBackupContractLimits.SafeTextLength)
            throw new ArgumentOutOfRangeException(nameof(NativeIdentity));
    }
}

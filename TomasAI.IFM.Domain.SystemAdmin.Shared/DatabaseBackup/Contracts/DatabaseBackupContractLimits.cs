using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

public static class DatabaseBackupContractLimits
{
    public const int IdentifierLength = 128;
    public const int SafeTextLength = 256;
    public const int DiagnosticReferenceLength = 512;
    public const int MaximumCollectionCount = 32;
    public const int MaximumPageSize = 200;
}

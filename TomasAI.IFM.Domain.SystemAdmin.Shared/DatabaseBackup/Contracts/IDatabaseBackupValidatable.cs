using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

public interface IDatabaseBackupValidatable
{
    void Validate();
}

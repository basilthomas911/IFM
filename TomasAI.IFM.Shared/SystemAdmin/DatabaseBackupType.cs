using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.SystemAdmin
{
    public enum DatabaseBackupType
    {
        Full,
        Diff
    }

    public static class DatabaseBackupTypeExtensions
    {
        public static string ToStringFast(this DatabaseBackupType value) => value switch
        {
            DatabaseBackupType.Full => nameof(DatabaseBackupType.Full),
            DatabaseBackupType.Diff => nameof(DatabaseBackupType.Diff),
            _ => value.ToString()
        };
    }
}

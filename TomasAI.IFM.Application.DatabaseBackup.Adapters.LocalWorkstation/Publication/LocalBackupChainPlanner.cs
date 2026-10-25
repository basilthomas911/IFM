using TomasAI.IFM.Application.DatabaseBackup.Policies;
﻿using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.DatabaseBackup.Policies;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Application.DatabaseBackup.Adapters.LocalWorkstation.Publication;

public sealed class LocalBackupChainPlanner(
    IDatabaseBackupCatalog catalog,
    LocalWorkstationSourceOptions sourceOptions) : IDatabaseBackupChainPlanner
{
    readonly DatabaseBackupChainPlanner _inner = new(catalog, new DatabaseBackupChainPolicy(
        sourceOptions.IncrementalEnabled,
        sourceOptions.MaximumIncrementalChainDepth,
        sourceOptions.MaximumIncrementalBaseAge));

    public ValueTask<DatabaseBackupLineage> PlanAsync(
        DatabaseBackupPlanningRequest request,
        CancellationToken cancellationToken)
        => _inner.PlanAsync(request, cancellationToken);
}

using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;

/// <summary>Validates replacement according to the backend's explicit ownership
/// contract. Native cleanup is acknowledged by Host after joining the old owner;
/// this check does not substitute for subsequent fresh four-leg/plan evidence.</summary>
public static class OptionRecoveryReplacementProof
{
    public static void Validate(MarketDataBackendStatus before, MarketDataDatasetStatus old,
        MarketDataBackendStatus after, MarketDataDatasetStatus replacement,
        MarketDataRecoveryRequest request, MarketDataRecoveryResult result)
    {
        if (before.RecoveryOwnership == MarketDataRecoveryOwnership.Process)
        {
            // Preserve the current C# backend's process replacement requirement.
            if (!replacement.Running || !replacement.Healthy || replacement.ProcessId <= 0 || replacement.ProcessId == old.ProcessId)
                throw new InvalidOperationException("Host did not prove immediate options-process replacement; deploy the matching updated Host and workers.");
            return;
        }
        if (before.RecoveryOwnership != MarketDataRecoveryOwnership.TokioDatasetOwner
            || after.RecoveryOwnership != before.RecoveryOwnership
            || before.HostInstanceId == Guid.Empty || after.HostInstanceId != before.HostInstanceId
            || !after.Ready || !result.Recovered || !result.CleanupConfirmed || !result.LiveFeedEnabled
            || request.Role != old.Role || request.Dataset != old.Dataset
            || request.ExpectedWorkerInstanceId != old.WorkerInstanceId || request.ExpectedGenerationId != old.GenerationId
            || result.OperationId != request.OperationId || result.Role != old.Role || result.Dataset != old.Dataset
            || replacement.Role != old.Role || replacement.Dataset != old.Dataset
            || after.ValueDate != request.ValueDate || replacement.ValueDate != request.ValueDate
            || !replacement.Running || !replacement.Healthy || replacement.Lifecycle != "Healthy" || replacement.ProcessId <= 0 || replacement.ProcessId != old.ProcessId
            || replacement.WorkerInstanceId == Guid.Empty || replacement.WorkerInstanceId == old.WorkerInstanceId
            || replacement.GenerationId == Guid.Empty || replacement.GenerationId == old.GenerationId
            || result.WorkerInstanceId != replacement.WorkerInstanceId || result.GenerationId != replacement.GenerationId)
            throw new InvalidOperationException("Host did not prove native options-owner cleanup, replacement and restoration.");
    }
}

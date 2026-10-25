using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Host;

public sealed partial class MarketDataWorkerHost
{
    public async Task<OptionObservationRepairResult> RepairOptionObservationsAsync(OptionObservationRepairRequest request, CancellationToken token)
    {
        var remaining = request.DeadlineUtc - DateTimeOffset.UtcNow;
        if (request.OperationId == Guid.Empty || request.GenerationId == Guid.Empty || request.Contracts.Length is < 1 or > 64
            || remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(2))
            throw new InvalidDataException("Exact option repair requires a current identity and the original recovery deadline.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(remaining);
        var reconnected = new List<string>(); var errors = new List<string>();
        foreach (var contract in request.Contracts.Distinct(StringComparer.Ordinal))
        {
            await optionLeaseChanges.WaitAsync(deadline.Token).ConfigureAwait(false);
            try
            {
                var worker = RequireWorker("Options");
                Pricing.WorkerOptionChainRequest? lease;
                lock (gate) lease = optionLeases.Values.FirstOrDefault(value => value.GenerationId == request.GenerationId
                    && value.ValueDate == request.ValueDate && value.SeparateContractConnection && value.Options.Length == 1
                    && value.LeaseExpiresAtUtc > DateTimeOffset.UtcNow && value.Options[0].Pricing.Contract.ContractId == contract);
                if (lease is null) { errors.Add(contract + ": no active exact-contract subscription in the requested generation"); continue; }
                var result = await worker.RunWithRecoveryDeadlineAsync(deadline.Token, budget =>
                    worker.AcquireOptionChainAsync(lease with { ReconnectExactContract = true }, budget)).WaitAsync(deadline.Token).ConfigureAwait(false);
                bool stillDesired;
                lock (gate) stillDesired = optionLeases.ContainsKey((worker.Current.WorkerInstanceId, lease.LeaseId));
                if (!stillDesired) errors.Add(contract + ": subscription released during repair");
                else if (!result.Active) errors.Add(contract + ": " + result.Failure?.Code + ": " + result.Failure?.Detail);
                else reconnected.Add(contract);
                logger.LogInformation("Option missing-leg repair; OperationId={OperationId}; Contract={Contract}; Generation={Generation}; Active={Active}; Error={Error}",
                    request.OperationId, contract, request.GenerationId, result.Active, result.Failure?.Detail);
            }
            catch (Exception error) when (!deadline.IsCancellationRequested)
            { errors.Add(contract + ": " + error.Message); logger.LogWarning(error, "Option missing-leg repair failed; OperationId={OperationId}; Contract={Contract}", request.OperationId, contract); }
            finally { optionLeaseChanges.Release(); }
        }
        return new(reconnected.ToArray(), errors.ToArray());
    }
}

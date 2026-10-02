namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Fail-closed activation boundary for the host recovery pipeline. The pipeline is not safe to
/// enable until generation-correlated downstream proof and candidate admission are composed.
/// </summary>
public static class ApiDatabentoRecoveryHostActivation
{
    public static bool Validate(bool enabled, bool isDevelopment, bool supervisedWorkers,
        bool downstreamProofAvailable)
    {
        if (!enabled) return false;
        if (!isDevelopment)
            throw new InvalidOperationException("Databento recovery pipeline opt-in is restricted to Development.");
        if (!supervisedWorkers)
            throw new InvalidOperationException("Databento recovery pipeline requires supervised workers.");
        if (!downstreamProofAvailable)
            throw new InvalidOperationException(
                "Databento recovery pipeline cannot start without generation-correlated downstream proof and candidate admission.");
        return true;
    }
}

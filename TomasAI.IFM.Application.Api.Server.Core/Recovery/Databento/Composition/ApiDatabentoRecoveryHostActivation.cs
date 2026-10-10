namespace TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Composition;

/// <summary>
/// Fail-closed activation boundary for the host recovery pipeline. The pipeline is not safe to
/// enable until exact-generation candidate admission is composed.
/// </summary>
public static class ApiDatabentoRecoveryHostActivation
{
    public static bool Validate(bool enabled, bool isDevelopment, bool supervisedWorkers,
        bool generationAdmissionAvailable)
    {
        if (!enabled) return false;
        if (!isDevelopment)
            throw new InvalidOperationException("Databento recovery pipeline opt-in is restricted to Development.");
        if (!supervisedWorkers)
            throw new InvalidOperationException("Databento recovery pipeline requires supervised workers.");
        if (!generationAdmissionAvailable)
            throw new InvalidOperationException(
                "Databento recovery pipeline cannot start without exact-generation candidate admission.");
        return true;
    }
}

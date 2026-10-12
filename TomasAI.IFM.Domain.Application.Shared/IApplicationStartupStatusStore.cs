using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Read/write boundary for the latest startup workflow snapshot.</summary>
public interface IApplicationStartupStatusStore
{
    ApplicationStartupStatus Current { get; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing published actor member name.")]
    void Set(ApplicationStartupStatus status);
    ValueTask<ApplicationStartupStatus> WaitForChangeAsync(
        ApplicationStartupStatus observed,
        CancellationToken cancellationToken);
}

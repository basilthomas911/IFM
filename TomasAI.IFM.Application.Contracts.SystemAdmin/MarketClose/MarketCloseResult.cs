[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Scope = "namespace", Target = "~N:TomasAI.IFM.Domain.SystemAdmin.Shared.MarketClose", Justification = "Preserve the published domain namespace.")]
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.MarketClose;

/// <summary>The backend-confirmed outcome of closing one authoritative futures session.</summary>
public sealed record MarketCloseResult(DateOnly ValueDate, Guid RunId, int PositionsFinalized, bool AlreadyCompleted);

/// <summary>Closes the API-owned current session without caller-supplied business inputs.</summary>
public interface IMarketCloseApi
{
    Task<MarketCloseResult> CloseMarketAsync();
}

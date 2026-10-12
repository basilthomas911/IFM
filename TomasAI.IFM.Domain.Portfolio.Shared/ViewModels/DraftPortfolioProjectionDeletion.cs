using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

/// <summary>Describes a complete draft portfolio projection deletion.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
public sealed record DraftPortfolioProjectionDeletion(
    int PortfolioId,
    int StateBucket,
    DraftFundProjectionDeletion[] Funds,
    long SourceEventId);

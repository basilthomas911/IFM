using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

/// <summary>Describes a draft financial-policy projection deletion.</summary>
public sealed record DraftPolicyProjectionDeletion(int PortfolioId, int PolicyId, long SourceEventId);

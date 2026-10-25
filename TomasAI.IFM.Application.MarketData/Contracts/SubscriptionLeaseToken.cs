using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Host epoch and lease incarnation fence stale clients. Renewal increments Version.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct SubscriptionLeaseToken(Guid HostEpochId, Guid LeaseId, long Version);

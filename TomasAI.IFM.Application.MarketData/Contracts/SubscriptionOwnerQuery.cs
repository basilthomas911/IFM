using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed record SubscriptionOwnerQuery(SubscriptionOwnerKey Owner, int Offset = 0, int PageSize = 100);

using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;
public enum SubscriptionResultCode
{
    Disabled, DesiredAccepted, Active, AlreadyOwned, Released, NotOwned, Recovering, Closed,
    Expired, Conflict, InvalidContract, CapacityExceeded, PricingUnavailable, StaleData,
    PersistenceUnavailable, OwnershipUnverified, Timeout, Cancelled, UnsupportedProtocol
}

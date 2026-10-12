using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
public enum DurableIntentResultCode
{
    Committed, AlreadyApplied, OperationConflict, RevisionConflict, StaleAuthority,
    AuthorityConflict, AuthorityGap, LeaseConflict, CapacityExceeded
}

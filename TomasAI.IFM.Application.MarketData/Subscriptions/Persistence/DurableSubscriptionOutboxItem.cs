using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

public sealed record DurableSubscriptionOutboxItem(
    Guid TransitionId, Guid OperationId, Guid CorrelationId, long Revision,
    string SourceId, long SourceVersion, DurableAuthorityStatus Status, string ReasonCode,
    DateTimeOffset CreatedAtUtc);

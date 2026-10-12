using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

public sealed record DurableSubscriptionOwner(string WorkflowType, string WorkflowId, string LegId);

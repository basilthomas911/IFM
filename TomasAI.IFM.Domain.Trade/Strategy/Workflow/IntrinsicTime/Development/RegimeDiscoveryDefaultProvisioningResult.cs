using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
/// <summary>Reports the actions performed while ensuring Development Regime Discovery defaults.</summary>
public sealed record RegimeDiscoveryDefaultProvisioningResult(int ExistingProfiles, int PublishedProfiles);

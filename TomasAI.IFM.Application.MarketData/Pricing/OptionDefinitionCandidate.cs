using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Exact normalized provider record and its independently requested reviewed mapping.</summary>
public sealed record OptionDefinitionCandidate(string ContractId, string MappingVersion,
    string DefinitionDigest, OptionContractDefinition Definition);

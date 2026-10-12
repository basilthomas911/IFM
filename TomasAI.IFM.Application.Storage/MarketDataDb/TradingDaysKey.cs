using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using MathNet.Numerics.Distributions;
using Microsoft.Extensions.Logging;
using System.Globalization;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Represents a unique key for identifying a range of trading days within a specific market and currency context.
/// </summary>
/// <remarks>This record is used to encapsulate the start and end dates of a trading period, along with the
/// associated market type and currency type. It is primarily intended for scenarios where trading day ranges need to be
/// uniquely identified or compared.</remarks>
/// <param name = "StartDate"></param>
/// <param name = "EndDate"></param>
/// <param name = "MarketType"></param>
/// <param name = "CurrencyType"></param>
record TradingDaysKey(DateOnly StartDate, DateOnly EndDate, MarketType MarketType, CurrencyType CurrencyType)
{
    public override string ToString() => $"{StartDate:yyyy-MM-dd}|{EndDate:yyyy-MM-dd}|{MarketType}|{CurrencyType}";
}

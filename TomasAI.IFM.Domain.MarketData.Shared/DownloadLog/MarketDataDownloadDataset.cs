using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

public enum MarketDataDownloadDataset { Unknown = 0, EconomicCalendar = 1, TreasuryCurve = 2 }

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
public enum MarketDataDownloadStatus { Unknown = 0, Completed = 1, Failed = 2 }

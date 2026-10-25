using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

public enum ReservationDisposition
{
    Unknown = 0,
    Committed = 1,
    IdempotentReplay = 2,
}

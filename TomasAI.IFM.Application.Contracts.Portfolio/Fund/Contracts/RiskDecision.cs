using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

public enum RiskDecision
{
    Unknown = 0,
    Approved = 1,
    Rejected = 2,
}

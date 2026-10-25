using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

public enum PortfolioBusinessIdentityKind
{
    Unknown = 0,
    Portfolio = 1,
    Fund = 2,
    Order = 3,
    Trade = 4,
    Policy = 5,
}

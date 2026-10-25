using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

public enum LedgerConfigurationAction
{
    Undefined = 0, CreateBook = 1, AddAccountVersion = 2, AddPostingRuleVersion = 3,
    RetireAccount = 4, RetirePostingRule = 5, OpenPeriod = 6, ClosePeriod = 7, ReopenPeriod = 8,
    RefreshAuthority = 9, Reconcile = 10, QualifyDevelopmentBook = 11
}

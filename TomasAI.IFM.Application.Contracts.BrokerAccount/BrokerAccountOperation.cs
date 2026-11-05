namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Identifies the operation that produced a committed account change.</summary>
public enum BrokerAccountOperation : byte
{
    Unknown = 0,
    RecordBrokerAccountSnapshot = 1,
    SubmitAccountQualificationEvidence = 2,
    AcceptAccountQualification = 3,
    RevokeAccountQualification = 4,
    SetManualTradingHold = 5,
    ReleaseManualTradingHold = 6,
    RequestBrokerAccountResynchronization = 7,
}

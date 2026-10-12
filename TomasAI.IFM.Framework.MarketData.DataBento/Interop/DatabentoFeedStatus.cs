using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public enum DatabentoFeedStatus
{
    Ok = 0,
    InvalidArgument = 1,
    InvalidState = 2,
    AbiMismatch = 3,
    NoMemory = 4,
    OsError = 5,
    DatabentoError = 6,
    Timeout = 7,
    BufferTooSmall = 8,
    RingOverrun = 9,
    ConnectionLimit = 10,
    RateLimit = 11,
    SymbolResolutionFailed = 12,
    IncompleteDefinitions = 13,
    NotSupported = 14,
    InternalError = 15,
    AffinityConfigurationFailed = 16,
    PriorityConfigurationFailed = 17,
    MemoryLockFailed = 18,
    NumaConfigurationFailed = 19,
    CoreIsolationFailed = 20,
    StopDrainIncomplete = 21,
    ConnectionHung = 22,
    PageConfigurationFailed = 23
}

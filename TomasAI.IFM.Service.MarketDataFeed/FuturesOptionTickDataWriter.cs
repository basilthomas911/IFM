using TomasAI.IFM.Shared.EventQueue;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Service.MarketDataFeed;

public class FuturesOptionTickDataWriter
{
    private static Dictionary<string, FuturesOptionTickDataWriter> _optionTickDataWriterMap;

    private ConcurrentEventQueue<FuturesOptionTickDataV2ReadModel> _futuresOptionTickDataEventQueue;
    private string _baseContractId;

    static FuturesOptionTickDataWriter()
    {
        _optionTickDataWriterMap = [];
    }

    public static Dictionary<string, FuturesOptionTickDataWriter> DataWriteMap => _optionTickDataWriterMap;

    /// <summary>
    /// create futuers option tick data writer
    /// </summary>
    /// <param name="contractId">option contract id</param>
    /// <param name="baseContractId">base futures contract id</param>
    /// <param name="futuresOptionTickDataAction"></param>
    /// <returns></returns>
    public static FuturesOptionTickDataWriter Create(string contractId, string baseContractId, Action<FuturesOptionTickDataV2ReadModel> futuresOptionTickDataAction)
    {
        if (!_optionTickDataWriterMap.ContainsKey(contractId))
            _optionTickDataWriterMap.Add(contractId, new FuturesOptionTickDataWriter(baseContractId, futuresOptionTickDataAction));
        return _optionTickDataWriterMap[contractId];
    }

    /// <summary>
    /// check for 
    /// </summary>
    /// <returns></returns>
    public static bool UpdateEventQueues()
    {
        foreach (var dataWriter in _optionTickDataWriterMap.Values)
            if (dataWriter.Updated)
                return true;
        return false;
    }

    public static void Clear() => _optionTickDataWriterMap.Clear();
    
    public static FuturesOptionTickDataWriter? GetOptionTickDataWriter(string contractId) => _optionTickDataWriterMap.ContainsKey(contractId) ? _optionTickDataWriterMap[contractId] : null;

    /// <summary>
    /// create futures option tick data writer
    /// </summary>
    /// <param name="futuresOptionTickDataAction"></param>
    public FuturesOptionTickDataWriter(string baseContractId, Action<FuturesOptionTickDataV2ReadModel> futuresOptionTickDataAction)
    {
        _baseContractId = baseContractId;
        _futuresOptionTickDataEventQueue = new ConcurrentEventQueue<FuturesOptionTickDataV2ReadModel>(o => futuresOptionTickDataAction(o));
        _futuresOptionTickDataEventQueue.Start();
    }

    /// <summary>
    /// return true if event queue was writen to
    /// </summary>
    public bool Updated {
        get
        {
            if (!_futuresOptionTickDataEventQueue.IsEmpty)
            {
                _futuresOptionTickDataEventQueue.Signal();
                return true;
            }
            return false;
        }
    }

    public string BaseContractId => _baseContractId;

    /// <summary>
    /// write futures option tick data to queue
    /// </summary>
    /// <param name="e"></param>
    public void Enqueue(FuturesOptionTickDataV2ReadModel e) => _futuresOptionTickDataEventQueue?.EnqueueForSignal(e);

}

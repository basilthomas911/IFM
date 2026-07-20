namespace TomasAI.IFM.Framework.Storage;

public class DataReaderOptions : IDataReaderOptions
{
    readonly DataReaderType _dataReaderType;
    readonly DataSourceType _dataSourceType;
    readonly Uri _dataSourceUri;
    readonly string _apiKey;
    readonly Dictionary<string, string> _optionsMap;

    public DataReaderOptions(string connectionString)
    {
        _optionsMap = ReadOptions(connectionString);
        _dataSourceUri = default!;
        if (_optionsMap.ContainsKey("Data Source"))
            _dataSourceUri = new Uri(_optionsMap["Data Source"]);
        _dataReaderType = DataReaderType.Csv;
        if (_optionsMap.TryGetValue("DataReaderType", out string? value))
            _ = Enum.TryParse(value, out _dataReaderType);
        _apiKey = string.Empty;
        if (_optionsMap.TryGetValue("ApiKey", out string? apiKey))
            _apiKey = apiKey;
        _dataSourceType = DataSourceType.Uri;
        DataReaderType = _dataReaderType;
    }

    public Uri Uri => _dataSourceUri;

    public DataReaderType DataReaderType { get; set; }  = DataReaderType.Csv;

    public DataSourceType DataSourceType => _dataSourceType;

    public string ApiKey => _apiKey; 

    static Dictionary<string,string> ReadOptions(string connectionString)
    {
        Dictionary<string, string> optionsMap = [];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var optionEntry = connectionString.Split([";" ], StringSplitOptions.RemoveEmptyEntries);
            foreach(var optionItems in optionEntry)
            {
                var optionItem = optionItems.Split([ " = " ], StringSplitOptions.RemoveEmptyEntries);
                if (optionItem.Length == 2)
                    optionsMap.Add(optionItem[0].Trim(), optionItem[1].Trim());
            }
        }
        return optionsMap;
    }
}

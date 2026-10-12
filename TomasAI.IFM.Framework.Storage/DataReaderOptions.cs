namespace TomasAI.IFM.Framework.Storage;

public class DataReaderOptions : IDataReaderOptions
{
    readonly DataSourceType _dataSourceType;
    readonly Uri _dataSourceUri;
    readonly string _apiKey;

    public DataReaderOptions(string connectionString)
    {
        var optionsMap = ReadOptions(connectionString);
        _dataSourceUri = default!;
        if (optionsMap.TryGetValue("Data Source", out var source))
            _dataSourceUri = new Uri(source);
        var dataReaderType = DataReaderType.Csv;
        if (optionsMap.TryGetValue("DataReaderType", out string? value))
            _ = Enum.TryParse(value, out dataReaderType);
        _apiKey = string.Empty;
        if (optionsMap.TryGetValue("ApiKey", out string? apiKey))
            _apiKey = apiKey;
        _dataSourceType = DataSourceType.Uri;
        DataReaderType = dataReaderType;
    }

    public Uri Uri => _dataSourceUri;

    public DataReaderType DataReaderType { get; set; }

    public DataSourceType DataSourceType => _dataSourceType;

    public string ApiKey => _apiKey;

    static Dictionary<string, string> ReadOptions(string connectionString)
    {
        Dictionary<string, string> optionsMap = [];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var optionEntry = connectionString.Split([";"], StringSplitOptions.RemoveEmptyEntries);
            foreach (var optionItems in optionEntry)
            {
                var optionItem = optionItems.Split([" = "], StringSplitOptions.RemoveEmptyEntries);
                if (optionItem.Length == 2)
                    optionsMap.Add(optionItem[0].Trim(), optionItem[1].Trim());
            }
        }
        return optionsMap;
    }
}

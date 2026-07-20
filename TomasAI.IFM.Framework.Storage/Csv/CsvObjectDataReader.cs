namespace TomasAI.IFM.Framework.Storage.Csv;

/// <summary>
/// create
/// </summary>
/// <param name="dataReader">sql server data reader</param>
/// <param name="resultTypeMap"></param>
public class CsvObjectDataReader<TResult>(ICsvDataReader dataReader, Dictionary<Type, object> resultTypeMap = default!) 
    : ObjectDataReader<TResult>(dataReader, resultTypeMap)
{
    public override Task<bool> ReadAsync() => throw new NotImplementedException();

}

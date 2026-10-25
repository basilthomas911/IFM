namespace TomasAI.IFM.Shared.Storage;

internal interface IParameterValueSource
{
    int? Count { get; }
    IEnumerable<object> Read();
}

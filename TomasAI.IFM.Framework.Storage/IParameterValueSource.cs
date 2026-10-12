namespace TomasAI.IFM.Framework.Storage;

internal interface IParameterValueSource
{
    int? Count { get; }
    IEnumerable<object> Read();
}

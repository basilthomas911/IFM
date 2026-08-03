namespace TomasAI.IFM.Framework.Storage;

internal interface IParameterValueSource
{
    int? Count { get; }
    IEnumerable<object> Read();
}

internal sealed class ParameterValueSource<TParam>(IEnumerable<TParam> source) : IParameterValueSource
{
    public int? Count { get; } = source.TryGetNonEnumeratedCount(out var count) ? count : null;

    public IEnumerable<object> Read()
    {
        foreach (var parameterValue in source)
        {
            yield return parameterValue is IBindValue bindValue
                ? bindValue.Bind()
                : parameterValue!;
        }
    }
}

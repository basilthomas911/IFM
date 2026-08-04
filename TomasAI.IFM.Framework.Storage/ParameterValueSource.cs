namespace TomasAI.IFM.Framework.Storage;

internal interface IParameterValueSource
{
    int? Count { get; }
    IEnumerable<object> Read();
}

internal interface IIndexedParameterValueSource : IParameterValueSource
{
    object ReadAt(int index);
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

internal sealed class IndexedParameterValueSource<TParam>(IReadOnlyList<TParam> source)
    : IIndexedParameterValueSource
    where TParam : struct, IBindValue
{
    public int? Count => source.Count;

    public object ReadAt(int index) => source[index].Bind();

    public IEnumerable<object> Read()
    {
        for (var index = 0; index < source.Count; index++)
            yield return source[index].Bind();
    }
}

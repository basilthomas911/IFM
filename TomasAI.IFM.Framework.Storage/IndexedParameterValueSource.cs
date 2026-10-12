namespace TomasAI.IFM.Framework.Storage;

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

namespace TomasAI.IFM.Framework.Storage;

internal interface IIndexedParameterValueSource : IParameterValueSource
{
    object ReadAt(int index);
}

namespace TomasAI.IFM.Shared.Storage;

internal interface IIndexedParameterValueSource : IParameterValueSource
{
    object ReadAt(int index);
}

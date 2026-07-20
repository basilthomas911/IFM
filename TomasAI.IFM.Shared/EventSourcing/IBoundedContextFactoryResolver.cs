namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextFactoryResolver
{
    object Resolve(Type genericType);
}

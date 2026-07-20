namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextCommandResolver
{
    object? Resolve(Type cmdHndlrType);
    void Add(Type cmdHndlrType, object cmdHndlr);
}

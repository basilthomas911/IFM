namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextParameterResolver
{
    object? Resolve(Type cmdHndlrType);
    void Add(Type cmdHndlrType, object cmdHndlr);
}

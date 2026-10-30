namespace TomasAI.IFM.Shared.Storage;

public interface IBindValue
{
    /// <summary>Returns positional C# values or provider-neutral BindParameter metadata. Driver-specific objects belong inside database providers.</summary>
    object Bind();
}

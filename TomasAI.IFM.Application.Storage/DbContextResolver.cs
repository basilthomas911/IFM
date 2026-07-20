using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// Provides functionality to resolve instances of object repositories based on their type.
/// </summary>
/// <remarks>This class is designed to resolve object repositories using a provided delegate function.  It is
/// typically used in scenarios where dependency injection or dynamic resolution of  repository instances is
/// required.</remarks>
/// <param name="resolver"></param>
public class DbContextResolver(Func<Type, object> resolver)
    : IDbContextResolver
{
    /// <summary>
    /// Resolves an instance of the specified repository type.
    /// </summary>
    /// <remarks>This method uses a type resolver to dynamically retrieve an implementation of the specified
    /// repository type. Ensure that the resolver is properly configured to handle the requested type.</remarks>
    /// <typeparam name="TRepo">The type of the repository to resolve. Must implement <see cref="IObjectRepository"/>.</typeparam>
    /// <returns>An instance of <see cref="IObjectRepository{TRepo}"/> corresponding to the specified repository type.</returns>
    public IObjectRepository<TRepo> Resolve<TRepo>() where TRepo : IObjectRepository
    {
        var objectRepoGenericType = typeof(IObjectRepository<>).MakeGenericType(typeof(TRepo));
        return (resolver?.Invoke(objectRepoGenericType) as IObjectRepository<TRepo>)!;
    }

}

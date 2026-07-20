using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Storage;

public class ObjectDataDbProvider(IObjectRepository repo, ILogger<DbProvider> logger) 
    : DbProvider(repo, logger)
{
}

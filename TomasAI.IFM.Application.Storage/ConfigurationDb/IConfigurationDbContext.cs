using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>
/// Combines Configuration database read and write capabilities.
/// </summary>
public interface IConfigurationDbContext :
    IObjectRepository<ConfigurationDbContext>,
    IConfigurationDbReadContext,
    IConfigurationDbWriteContext
{
    /// <summary>Gets the Configuration database read capability.</summary>
    IConfigurationDbReadContext DbReader { get; }

    /// <summary>Gets the Configuration database write capability.</summary>
    IConfigurationDbWriteContext DbWriter { get; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage;

/// <summary>
/// Defines a collection of database connection settings that can be accessed by name and extended with additional
/// connections.
/// </summary>
/// <remarks>Implementations of this interface provide read-only access to the set of connection settings, as well
/// as a method to create a new collection with an added connection. The interface is typically used to manage and
/// retrieve connection information for multiple databases within an application.</remarks>
public interface IDbConnectionSettings
{
    IDbConnectionSetting this[string connectionName] { get; }
    int Count { get; }
    IDbConnectionSettings Add(string connectionName, string connectionString, string providerName);
}

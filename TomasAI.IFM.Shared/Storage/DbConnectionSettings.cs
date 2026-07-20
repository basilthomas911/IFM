using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage;

/// <summary>
/// Represents a collection of database connection settings, accessible by connection name.
/// </summary>
/// <remarks>Use this class to manage multiple named database connection configurations within an application.
/// Connection settings can be added and retrieved by name. This class is typically used to centralize and organize
/// connection information for different databases or environments.</remarks>
public class DbConnectionSettings : IDbConnectionSettings
{
    Dictionary<string, IDbConnectionSetting> _connSettings;

    /// <summary>
    /// create connection settings 
    /// </summary>
    public DbConnectionSettings()
    {
        _connSettings = [];
    }

    /// <summary>
    /// return db connection setting by connection name
    /// </summary>
    /// <param name="connectionName"></param>
    /// <returns></returns>
    public IDbConnectionSetting this[string connectionName]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentNullException("connectionName", "DbConnectionSettings.this[]: 'connectionName' parameter is empty");
            return (_connSettings.ContainsKey(connectionName)
                ? _connSettings[connectionName]
                : default)!;
        }
    }

    public int Count => _connSettings.Count;

    public IDbConnectionSettings Add(string connectionName, string connectionString, string providerName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            throw new ArgumentNullException("connectionName", "DbConnectionSettings.Add: 'connectionName' parameter is empty");
        if (_connSettings.ContainsKey(connectionName))
            throw new InvalidOperationException($"DbConnectionSettings.Add: connection settings already exists for: '{connectionName}");
        if (!string.IsNullOrWhiteSpace(connectionString))
            _connSettings.Add(connectionName, new DbConnectionSetting(connectionName, connectionString, providerName));
        return this;
    }
}

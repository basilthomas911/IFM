using System.Data;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides a context for executing stored procedures within an object data repository.
/// </summary>
/// <remarks>This class is designed to facilitate the execution of stored procedures by setting the appropriate
/// command type and managing stored procedure-specific behaviors, such as parameter naming conventions.</remarks>
/// <param name="db"></param>
/// <param name="logger"></param>
/// <param name="storedProcName"></param>
public class ObjectDataStoredProcedureContext(IObjectRepository db, ILogger<DbProvider> logger, string storedProcName = null!)
    : ObjectDataRepositoryContext(db, logger  )
{
    readonly string _storedProcName = storedProcName;

    public new string CommandText => _storedProcName;   

    /// <summary>
    /// set command type to stored procedure
    /// </summary>
    /// <param name="cmd"></param>
    public override void SetCommand(IDbCommand cmd)
    {
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = _storedProcName;
    }

    /// <summary>
    /// return command text as stored procedure name
    /// </summary>
    /// <returns></returns>
    public override string GetCommandText() => _storedProcName;

    public override CommandType GetCommandType() => CommandType.StoredProcedure;

    /// <summary>
    /// return provider based parameter name
    /// </summary>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    public override string GetParameterName(string parameterName) 
        => Repository.ProviderName switch {
            "System.Data.Cassandra" => parameterName,
            "System.Data.Scylla" => parameterName,
            "System.Data.Postgres" => $"_{parameterName}",
            "System.Data.SqlServer" => $"@{parameterName}",
            _ or null => throw new ArgumentException("ObjectDataStoredProcedureContext: parameter name is empty"),
        };

}

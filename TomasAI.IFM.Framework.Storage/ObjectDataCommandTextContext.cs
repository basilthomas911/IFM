using Microsoft.Extensions.Logging;
using System.Data;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataCommandTextContext : ObjectDataRepositoryContext
    {
        string _cmdText;

        /// <summary>
        /// create object command text context
        /// </summary>
        /// <param name="db"></param>
        public ObjectDataCommandTextContext(IObjectRepository db, ILogger<DbProvider> logger, string cmdText = null!)
            :base(db, logger)
        {
            _cmdText = cmdText;
        }

        public string CommandText => _cmdText;

        /// <summary>
        /// set command type to command text
        /// </summary>
        /// <param name="cmd"></param>
        public override void SetCommand(IDbCommand cmd)
        {
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = _cmdText;
        }

        /// <summary>
        /// return command text 
        /// </summary>
        /// <returns></returns>
        public override string GetCommandText() => _cmdText;

        public override CommandType GetCommandType() => CommandType.Text;

        /// <summary>
        /// return required sql parameter name based on underlying sql data provider
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        public override string GetParameterName(string parameterName)
                 => Repository.ProviderName switch
                 {
                     "System.Data.Cassandra" => parameterName,
                     "System.Data.Scylla" => parameterName,
                     "System.Data.Postgres" => $"_{parameterName}",
                     "System.Data.SqlServer" => $"@{parameterName}",
                     _ or null => throw new ArgumentException("ObjectDataStoredProcedureContext: parameter name is empty"),
                 };
    }
}

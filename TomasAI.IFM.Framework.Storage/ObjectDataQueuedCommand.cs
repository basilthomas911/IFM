using System;
using System.Data;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataQueuedCommand
    {
        CommandType _commandType;
        string _commandText;
        DbParameter[]? _parameters;

        public CommandType CommandType => _commandType;
        public string CommandText => _commandText;
        public DbParameter[]? Parameters => _parameters;

        public ObjectDataQueuedCommand(CommandType commandType, string commandText, DbParameter[]? parameters)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                throw new ArgumentException("ObjectDataCommand: commandText parameter is empty");
            _commandType = commandType;
            _commandText = commandText;
            _parameters = parameters;
        }
    }
}

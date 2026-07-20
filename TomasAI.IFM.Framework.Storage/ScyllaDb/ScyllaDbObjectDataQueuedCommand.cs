using System;
using System.Data;
using System.Data.Common;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb
{
    public class ScyllaDbObjectDataQueuedCommand
    {
        const string ClassName = nameof(ScyllaDbObjectDataQueuedCommand);   

        CommandType _commandType;
        string _commandText;
        List<object>? _bindValues;

        public CommandType CommandType => _commandType;
        public string CommandText => _commandText;
        public List<object>? BindValues => _bindValues;

        public ScyllaDbObjectDataQueuedCommand(CommandType commandType, string commandText, List<object>? bindValues)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                throw new StorageException($"{ClassName}.constructor: commandText parameter is empty");
            _commandType = commandType;
            _commandText = commandText;
            _bindValues = bindValues;
        }
    }
}
using System;
using System.Data;
using System.Data.Common;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb
{
    public class ScyllaDbObjectDataQueuedCommand : IObjectDataQueuedCommandMetadata
    {
        const string ClassName = nameof(ScyllaDbObjectDataQueuedCommand);   

        CommandType _commandType;
        string _commandText;
        List<object>? _bindValues;
        readonly string? _providerName;
        readonly object? _connectionIdentity;

        public CommandType CommandType => _commandType;
        public string CommandText => _commandText;
        public List<object>? BindValues => _bindValues;
        string? IObjectDataQueuedCommandMetadata.ProviderName => _providerName;
        object? IObjectDataQueuedCommandMetadata.ConnectionIdentity => _connectionIdentity;

        public ScyllaDbObjectDataQueuedCommand(CommandType commandType, string commandText, List<object>? bindValues)
            : this(commandType, commandText, bindValues, null, null)
        {
        }

        internal ScyllaDbObjectDataQueuedCommand(
            CommandType commandType,
            string commandText,
            List<object>? bindValues,
            string? providerName,
            object? connectionIdentity)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                throw new StorageException($"{ClassName}.constructor: commandText parameter is empty");
            _commandType = commandType;
            _commandText = commandText;
            _bindValues = bindValues;
            _providerName = providerName;
            _connectionIdentity = connectionIdentity;
        }
    }
}

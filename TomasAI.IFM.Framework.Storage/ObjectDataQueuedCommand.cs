using System;
using System.Data;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataQueuedCommand : IObjectDataQueuedCommandMetadata
    {
        CommandType _commandType;
        string _commandText;
        DbParameter[]? _parameters;
        readonly string? _providerName;
        readonly object? _connectionIdentity;

        public CommandType CommandType => _commandType;
        public string CommandText => _commandText;
        public DbParameter[]? Parameters => _parameters;
        string? IObjectDataQueuedCommandMetadata.ProviderName => _providerName;
        object? IObjectDataQueuedCommandMetadata.ConnectionIdentity => _connectionIdentity;

        public ObjectDataQueuedCommand(CommandType commandType, string commandText, DbParameter[]? parameters)
            : this(commandType, commandText, parameters, null, null)
        {
        }

        internal ObjectDataQueuedCommand(
            CommandType commandType,
            string commandText,
            DbParameter[]? parameters,
            string? providerName,
            object? connectionIdentity)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                throw new ArgumentException("ObjectDataCommand: commandText parameter is empty");
            _commandType = commandType;
            _commandText = commandText;
            _parameters = parameters;
            _providerName = providerName;
            _connectionIdentity = connectionIdentity;
        }
    }
}

using System;
using System.Data;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataQueuedCommand : IObjectDataQueuedCommandMetadata
    {
        readonly string _commandName;
        CommandType _commandType;
        string _commandText;
        DbParameter[]? _parameters;
        readonly string? _providerName;
        readonly object? _connectionIdentity;

        public string CommandName => _commandName;
        public CommandType CommandType => _commandType;
        public string CommandText => _commandText;
        public string CommandLogText =>
            $"command name: {CommandName}{Environment.NewLine}{CommandText}";
        public DbParameter[]? Parameters => _parameters;
        string? IObjectDataQueuedCommandMetadata.ProviderName => _providerName;
        object? IObjectDataQueuedCommandMetadata.ConnectionIdentity => _connectionIdentity;

        public ObjectDataQueuedCommand(
            string commandName,
            CommandType commandType,
            string commandText,
            DbParameter[]? parameters)
            : this(commandName, commandType, commandText, parameters, null, null)
        {
        }

        internal ObjectDataQueuedCommand(
            string commandName,
            CommandType commandType,
            string commandText,
            DbParameter[]? parameters,
            string? providerName,
            object? connectionIdentity)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("ObjectDataCommand: commandName parameter is empty");
            if (string.IsNullOrWhiteSpace(commandText))
                throw new ArgumentException("ObjectDataCommand: commandText parameter is empty");
            _commandName = commandName;
            _commandType = commandType;
            _commandText = commandText;
            _parameters = parameters;
            _providerName = providerName;
            _connectionIdentity = connectionIdentity;
        }
    }
}

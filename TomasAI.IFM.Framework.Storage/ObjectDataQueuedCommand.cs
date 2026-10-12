using System;
using System.Data;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataQueuedCommand : IObjectDataQueuedCommandMetadata
    {
        readonly string _commandName;
        readonly CommandType _commandType;
        readonly string _commandText;
        readonly DbParameter[]? _parameters;
        internal IReadOnlyList<DbParameter[]?> ParameterSets { get; private init; } = Array.Empty<DbParameter[]?>();
        readonly string? _providerName;
        readonly object? _connectionIdentity;

        public string CommandName => _commandName;
        public CommandType CommandType => _commandType;
        public string CommandText => _commandText;
        public string CommandLogText =>
            $"command name: {CommandName}{Environment.NewLine}{CommandText}";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
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
            string commandName, CommandType commandType, string commandText,
            IReadOnlyList<DbParameter[]?> parameterSets, string? providerName, object? connectionIdentity)
            : this(commandName, commandType, commandText, parameterSets.Count > 0 ? parameterSets[0] : null, providerName, connectionIdentity)
        {
            ParameterSets = parameterSets;
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
                throw new ArgumentException("ObjectDataCommand: commandName parameter is empty",nameof(commandName));
            if (string.IsNullOrWhiteSpace(commandText))
                throw new ArgumentException("ObjectDataCommand: commandText parameter is empty",nameof(commandText));
            _commandName = commandName;
            _commandType = commandType;
            _commandText = commandText;
            _parameters = parameters;
            ParameterSets = new[] { parameters };
            _providerName = providerName;
            _connectionIdentity = connectionIdentity;
        }
    }
}

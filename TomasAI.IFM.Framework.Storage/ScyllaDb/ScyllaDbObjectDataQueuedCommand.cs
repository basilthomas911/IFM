using System;
using System.Data;
using System.Data.Common;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb
{
    public class ScyllaDbObjectDataQueuedCommand : IObjectDataQueuedCommandMetadata
    {
        const string ClassName = nameof(ScyllaDbObjectDataQueuedCommand);

        readonly string _commandName;
        readonly CommandType _commandType;
        readonly string _commandText;
        readonly List<object>? _bindValues;
        readonly string? _providerName;
        readonly object? _connectionIdentity;

        public string CommandName => _commandName;
        public CommandType CommandType => _commandType;
        public string CommandText => _commandText;
        public string CommandLogText =>
            $"command name: {CommandName}{Environment.NewLine}{CommandText}";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        public List<object>? BindValues => _bindValues;
        string? IObjectDataQueuedCommandMetadata.ProviderName => _providerName;
        object? IObjectDataQueuedCommandMetadata.ConnectionIdentity => _connectionIdentity;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        public ScyllaDbObjectDataQueuedCommand(
            string commandName,
            CommandType commandType,
            string commandText,
            List<object>? bindValues)
            : this(commandName, commandType, commandText, bindValues, null, null)
        {
        }

        internal ScyllaDbObjectDataQueuedCommand(
            string commandName,
            CommandType commandType,
            string commandText,
            List<object>? bindValues,
            string? providerName,
            object? connectionIdentity)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new StorageException($"{ClassName}.constructor: commandName parameter is empty");
            if (string.IsNullOrWhiteSpace(commandText))
                throw new StorageException($"{ClassName}.constructor: commandText parameter is empty");
            _commandName = commandName;
            _commandType = commandType;
            _commandText = commandText;
            _bindValues = bindValues;
            _providerName = providerName;
            _connectionIdentity = connectionIdentity;
        }
    }
}

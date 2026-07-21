using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

public class BackupDatabasesViewModel
{
    static Dictionary<string, Queue<string>> _statusMessagesMap = null!;
    readonly IAppRoot _appRoot;
    readonly IStatusConsoleEventProducer _statusConsoleLog;
    List<string> _databaseNames;
    DatabaseBackupType _backupType;
    int _commandTimeout;

    public BackupDatabasesViewModel(IAppRoot appRoot, IStatusConsoleEventProducer statusConsoleLog)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _statusConsoleLog = statusConsoleLog ?? throw new ArgumentNullException(nameof(statusConsoleLog));
        _statusMessagesMap = new Dictionary<string, Queue<string>>();
        _databaseNames = new List<string>();
    }

    public string[] DatabaseNames => _databaseNames.ToArray();

    public Action<string, string> OnStatusMessagesUpdate = null!;
    public Action OnDatabaseNamesLoaded = null!;
    public Action<string> OnDatabaseBackupComplete = null!;

    public string[]? GetStatusMessages(string databaseName) => _statusMessagesMap.ContainsKey(databaseName) ? _statusMessagesMap[databaseName].ToArray() : default(string[]);
    public void DatabaseBackupCompleted() { }
    public void SetBackupType(bool fullBackupSelected) => _backupType = fullBackupSelected ? DatabaseBackupType.Full : DatabaseBackupType.Diff;
    public void SetCommandTimeout(decimal commandTimeout) => _commandTimeout = Convert.ToInt32(commandTimeout);

    public void SetStatusMessage(string databaseName, string statusMessage)
    {
        if (!_statusMessagesMap.ContainsKey(databaseName))
            _statusMessagesMap.Add(databaseName, new Queue<string>());
        _statusMessagesMap[databaseName].Enqueue(statusMessage);
    }

    public void LoadDatabaseNames()
        => _appRoot.GetModel<SystemAdminModel>().Execute(async model =>
            await model.LoadDatabaseNamesAsync(databaseNames => {
               _databaseNames.Clear();
               if (databaseNames != null)
                   _databaseNames.AddRange(databaseNames);
               OnDatabaseNamesLoaded?.Invoke();
        }));

    public void RunDatabaseBackup(ICollection<string> databaseNames)
        =>  _appRoot.GetModel<SystemAdminModel>().Execute(async model => {
            _statusMessagesMap.Clear(); 
            foreach (var databaseName in databaseNames)
                await model.BackupDatabaseAsync(databaseName, _backupType, _commandTimeout * 60);
        });

    public void StartSystemAdminEventConsumer()
        =>  _appRoot.GetModel<SystemAdminModel>().Execute(async model => {
                await model.StartSystemAdminEventConsumer(
                    infoMsgAction: o => OnStatusMessagesUpdate?.Invoke(o.DatabaseName, o.InfoMessage),
                    completedAction: o => OnDatabaseBackupComplete?.Invoke(o.DatabaseName));
            });

    public void StopSystemAdminEventConsumer()
        => _appRoot.GetModel<SystemAdminModel>().Execute(async model => {
            _statusMessagesMap.Clear();
            await model.StopSystemAdminEventConsumer();
        });

}

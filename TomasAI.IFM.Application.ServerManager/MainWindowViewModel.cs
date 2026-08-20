using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TomasAI.IFM.Application.ServerManager;

public sealed partial class MainWindowViewModel : ObservableObject, IMainWindowViewModel
{
    private readonly int _maximumLogEntries;
    private readonly IUiDispatcher _dispatcher;
    private readonly ConcurrentQueue<ManagedProcessLogEntry> _pendingEntries = new();
    private int _pendingCount;
    private int _drainScheduled;
    private int _droppedSinceLastDrain;

    public MainWindowViewModel(ServerManagerOptions options, IUiDispatcher dispatcher)
    {
        _maximumLogEntries = options.MaximumLogEntries;
        _dispatcher = dispatcher;
        ConsoleStatus = new ObservableCollection<StatusLog>();
    }

    public ObservableCollection<StatusLog> ConsoleStatus { get; }

    [ObservableProperty]
    private Visibility _consoleVisibility;

    [ObservableProperty]
    private WindowState _consoleWindowState;

    public void AddLog(ManagedProcessLogEntry entry)
    {
        _pendingEntries.Enqueue(entry);
        var pendingCount = Interlocked.Increment(ref _pendingCount);
        while (pendingCount > _maximumLogEntries && _pendingEntries.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _pendingCount);
            Interlocked.Increment(ref _droppedSinceLastDrain);
            pendingCount = Volatile.Read(ref _pendingCount);
        }

        if (Interlocked.CompareExchange(ref _drainScheduled, 1, 0) == 0)
        {
            _dispatcher.Post(DrainPendingEntries);
        }
    }

    private void DrainPendingEntries()
    {
        while (true)
        {
            var dropped = Interlocked.Exchange(ref _droppedSinceLastDrain, 0);
            while (_pendingEntries.TryDequeue(out var entry))
            {
                Interlocked.Decrement(ref _pendingCount);
                Insert(entry);
            }

            if (dropped > 0)
            {
                Insert(new ManagedProcessLogEntry(
                    DateTimeOffset.Now,
                    "manager",
                    "Server Manager",
                    ManagedProcessLogStream.Manager,
                    $"Dropped {dropped} pending log entries because the UI log buffer was full."));
            }

            Interlocked.Exchange(ref _drainScheduled, 0);
            if (_pendingEntries.IsEmpty || Interlocked.CompareExchange(ref _drainScheduled, 1, 0) != 0)
            {
                return;
            }
        }
    }

    private void Insert(ManagedProcessLogEntry entry)
    {
        ConsoleStatus.Insert(0, new StatusLog
        {
            Timestamp = entry.Timestamp,
            ProcessName = entry.ProcessName,
            Stream = entry.Stream,
            Message = entry.Message
        });

        while (ConsoleStatus.Count > _maximumLogEntries)
        {
            ConsoleStatus.RemoveAt(ConsoleStatus.Count - 1);
        }
    }
}

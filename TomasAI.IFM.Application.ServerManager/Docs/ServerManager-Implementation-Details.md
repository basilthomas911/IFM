# IFM Server Manager — Implementation Details

## Purpose

`TomasAI.IFM.Application.ServerManager` is a Windows desktop process supervisor for selected IFM server executables. It combines:

- a WPF application and log-console window;
- a Windows Forms system-tray icon and context menu;
- configuration-driven child-process launch settings;
- redirected standard-output capture; and
- reset and shutdown handling for the managed processes.

At startup it launches the configured Telemetry, Event, and Predictive Model processes. Their standard-output lines are labeled by server type and inserted at the top of the WPF status grid.

## Complete project folder map

The following tree documents every directory currently present beneath the project root, from root to each leaf. `bin` and `obj` are generated trees; the framework/configuration combinations they contain reflect prior local builds and may change over time.

```text
TomasAI.IFM.Application.ServerManager/                   Project root
├── Docs/                                                Maintained project documentation
├── Properties/                                          Deployment metadata
│   └── PublishProfiles/                                 ClickOnce and folder profiles
├── Resources/                                           Application icon assets
├── bin/                                                 Generated build output
│   ├── Debug/
│   │   ├── net10.0-windows7.0/
│   │   │   └── runtimes/
│   │   │       └── win/
│   │   │           └── lib/
│   │   │               ├── net10.0/                     Debug .NET 10 runtime asset leaf
│   │   │               └── net8.0/                      Debug .NET 8 compatibility asset leaf
│   │   └── net8.0-windows7.0/
│   │       └── runtimes/
│   │           └── win/
│   │               └── lib/
│   │                   └── net8.0/                      Legacy Debug .NET 8 runtime asset leaf
│   └── Release/
│       └── net10.0-windows7.0/
│           └── runtimes/
│               └── win/
│                   └── lib/
│                       └── net10.0/                     Release .NET 10 runtime asset leaf
└── obj/                                                 Generated compiler/MSBuild state
    ├── Debug/
    │   ├── net10.0-windows7.0/
    │   │   ├── ref/                                     Debug .NET 10 reference assembly leaf
    │   │   └── refint/                                  Debug .NET 10 internal-reference leaf
    │   └── net8.0-windows7.0/
    │       ├── ref/                                     Legacy Debug .NET 8 reference leaf
    │       └── refint/                                  Legacy Debug .NET 8 internal-reference leaf
    └── Release/
        └── net10.0-windows7.0/
            ├── ref/                                     Release .NET 10 reference assembly leaf
            └── refint/                                  Release .NET 10 internal-reference leaf
```

### Folder responsibilities

| Folder | Kind | Responsibility |
| --- | --- | --- |
| Project root | Source | Contains application startup, views, view models, process-management classes, configuration, resources, and the project definition. |
| `Docs/` | Documentation leaf | Contains this implementation and complete structure reference. |
| `Properties/` | Source metadata | Groups publish/deployment metadata. |
| `Properties/PublishProfiles/` | Source metadata leaf | Contains ClickOnce and folder-publish profiles. |
| `Resources/` | Source asset leaf | Contains the `.ico` file used by the executable and tray icon resource. |
| `bin/` | Generated | Root of compiled application output. Do not edit or commit generated contents. |
| `bin/Debug/` | Generated | Debug-configuration outputs. |
| `bin/Debug/net10.0-windows7.0/` | Generated | Current Debug output for the project's .NET 10 Windows target. |
| `bin/Debug/net10.0-windows7.0/runtimes/` | Generated | Runtime-specific Debug dependencies. |
| `bin/Debug/net10.0-windows7.0/runtimes/win/` | Generated | Windows runtime dependency branch. |
| `bin/Debug/net10.0-windows7.0/runtimes/win/lib/` | Generated | Framework-grouped managed runtime libraries. |
| `bin/Debug/net10.0-windows7.0/runtimes/win/lib/net10.0/` | Generated leaf | .NET 10 Windows runtime libraries. |
| `bin/Debug/net10.0-windows7.0/runtimes/win/lib/net8.0/` | Generated leaf | .NET 8-compatible dependency assets copied into the .NET 10 Debug output. |
| `bin/Debug/net8.0-windows7.0/` | Generated | Output retained from an earlier .NET 8 Debug build. |
| `bin/Debug/net8.0-windows7.0/runtimes/` | Generated | Legacy runtime-specific dependency branch. |
| `bin/Debug/net8.0-windows7.0/runtimes/win/` | Generated | Legacy Windows runtime branch. |
| `bin/Debug/net8.0-windows7.0/runtimes/win/lib/` | Generated | Legacy framework-grouped managed libraries. |
| `bin/Debug/net8.0-windows7.0/runtimes/win/lib/net8.0/` | Generated leaf | Legacy .NET 8 Windows runtime libraries. |
| `bin/Release/` | Generated | Release-configuration outputs. |
| `bin/Release/net10.0-windows7.0/` | Generated | Current Release output for the .NET 10 Windows target. |
| `bin/Release/net10.0-windows7.0/runtimes/` | Generated | Runtime-specific Release dependencies. |
| `bin/Release/net10.0-windows7.0/runtimes/win/` | Generated | Windows Release runtime branch. |
| `bin/Release/net10.0-windows7.0/runtimes/win/lib/` | Generated | Framework-grouped Release runtime libraries. |
| `bin/Release/net10.0-windows7.0/runtimes/win/lib/net10.0/` | Generated leaf | .NET 10 Windows Release runtime libraries. |
| `obj/` | Generated | Root of restore data and compiler/MSBuild intermediates. Do not edit or commit generated contents. |
| `obj/Debug/` | Generated | Debug compiler/MSBuild intermediates. |
| `obj/Debug/net10.0-windows7.0/` | Generated | Current Debug .NET 10 generated sources, caches, and intermediate assemblies. |
| `obj/Debug/net10.0-windows7.0/ref/` | Generated leaf | Current Debug reference assembly. |
| `obj/Debug/net10.0-windows7.0/refint/` | Generated leaf | Current Debug internal reference assembly. |
| `obj/Debug/net8.0-windows7.0/` | Generated | Intermediates retained from an earlier Debug .NET 8 build. |
| `obj/Debug/net8.0-windows7.0/ref/` | Generated leaf | Legacy Debug reference assembly. |
| `obj/Debug/net8.0-windows7.0/refint/` | Generated leaf | Legacy Debug internal reference assembly. |
| `obj/Release/` | Generated | Release compiler/MSBuild intermediates. |
| `obj/Release/net10.0-windows7.0/` | Generated | Current Release .NET 10 generated sources, caches, and intermediate assemblies. |
| `obj/Release/net10.0-windows7.0/ref/` | Generated leaf | Current Release reference assembly. |
| `obj/Release/net10.0-windows7.0/refint/` | Generated leaf | Current Release internal reference assembly. |

## Maintained file inventory

| File | Responsibility |
| --- | --- |
| `App.xaml` | Declares the WPF application; it has no `StartupUri`, so startup is controlled in code. |
| `App.xaml.cs` | Loads base configuration, creates the DI container, and initializes `ServerLauncherContext`. |
| `AssemblyInfo.cs` | Configures WPF theme-resource lookup. |
| `MainWindow.xaml` | Defines the tool-window console and its status `DataGrid`. |
| `MainWindow.xaml.cs` | Initializes the view and assigns its injected view model as `DataContext`; its own log/clear methods are not used by the launcher context. |
| `IMainWindowViewModel.cs` | Defines console visibility, window-state, and log-ingestion behavior. |
| `MainWindowViewModel.cs` | Maintains the observable status collection and marshals log insertion onto the WPF dispatcher. |
| `StatusLog.cs` | Represents one displayed status-log row. |
| `ConsoleVisibilityModel.cs` | Observable wrapper around WPF `Visibility`; currently not used by the window or DI graph. |
| `ConsoleWindowStateModel.cs` | Observable wrapper around WPF `WindowState`; currently not used by the window or DI graph. |
| `ServerLauncherContext.cs` | Owns the tray icon/menu, console visibility commands, child-launcher lifecycle, reset, and exit behavior. |
| `ServerLauncher.cs` | Starts one configured process on a background task, redirects standard output, waits for exit, and kills it on disposal. |
| `Resource1.resx` | Embeds the `AppIcon` resource used by the tray notification icon. |
| `Resource1.Designer.cs` | Generated strongly typed accessor for `Resource1.resx`. |
| `Resources/AppIcon.ico` | Application and tray icon source asset. |
| `appsettings.json` | Required Server Manager launch configuration and Serilog configuration. |
| `appsettings.Development.json` | Additional development application settings; not loaded by current `App` startup code. |
| `appsettings.Production.json` | Additional production application settings; not loaded by current `App` startup code. |
| `Properties/PublishProfiles/ClickOnceProfile.pubxml` | ClickOnce deployment settings. |
| `Properties/PublishProfiles/FolderProfile.pubxml` | Folder-publish settings. |
| `TomasAI.IFM.Application.ServerManager.csproj` | Defines the Windows desktop target, resources, packages, project reference, and content-copy behavior. |
| `Docs/ServerManager-Implementation-Details.md` | This document. |

## Application architecture

The project intentionally mixes two Windows UI stacks:

| Technology | Use |
| --- | --- |
| WPF | Application lifecycle, main console window, data binding, dispatcher, visibility, and window state. |
| Windows Forms | `NotifyIcon`, tray context menu, and `ApplicationContext` base class. |
| Microsoft DI | Singleton view model and main-window construction. |
| Microsoft Configuration | JSON launch-setting binding. |
| `System.Diagnostics.Process` | Child-process creation, output capture, wait, and termination. |

`TomasAI.IFM.Shared` supplies `ServerLogType`, which labels log entries as Telemetry, Event, or Query.

## Startup and dependency injection

WPF calls `App.OnStartup` because `App.xaml` has no startup window URI:

1. Create a `ConfigurationBuilder` rooted at `Directory.GetCurrentDirectory()`.
2. Load required `appsettings.json` with `reloadOnChange: true`.
3. Build a minimal service collection.
4. Register `IMainWindowViewModel` as singleton `MainWindowViewModel`.
5. Register `MainWindow` as a singleton.
6. Build the service provider.
7. Instantiate `ServerLauncherContext`, which creates the tray icon and launches the console and servers.

The current startup code does **not** load `appsettings.Development.json` or `appsettings.Production.json`, does not read a hosting environment, and does not add environment variables or command-line configuration providers.

## Tray and console behavior

`ServerLauncherContext` creates a visible tray icon with three commands:

| Interaction | Behavior |
| --- | --- |
| `View Console` | Disables itself, makes the WPF window visible, and maximizes it. |
| `Minimize Console` | Re-enables `View Console`, hides the WPF window, and minimizes it. |
| `Reset` | Stops all three launchers, then starts them again from configuration. |
| Tray-icon double-click | Performs `View Console`. |

Startup sets the console to hidden/minimized, resolves the singleton window, and calls `Show()`. The window remains part of the WPF application even while hidden.

The console contains one non-auto-generated `DataGrid` column bound to `StatusLog.LogEntry`. `MainWindowViewModel.AddServerLog` invokes the WPF dispatcher and inserts each new entry at index zero, so the newest output appears first. The displayed text is formed as `<ServerLogType> <line>`.

There is no tray-menu `Exit` item. Application exit—such as closing the WPF window under the default WPF shutdown mode—triggers the subscribed cleanup handler, which stops launchers, hides the tray icon, and calls `Shutdown()`.

## Managed-server startup sequence

`StartServers` resolves `IMainWindowViewModel` and launches servers in this order:

```text
Telemetry process
  └─ Fixed 5-second synchronous wait
       └─ Event process
            └─ Predictive Model process
```

| Logical server | Configuration keys | Displayed log type |
| --- | --- | --- |
| Telemetry | `ServerManager:Telemetry:WorkingDirectory`, `ServerManager:Telemetry:ExeName` | `Telemetry` |
| Event | `ServerManager:Event:WorkingDirectory`, `ServerManager:Event:ExeName` | `Event` |
| Predictive Model | `ServerManager:PredictiveModel:WorkingDirectory`, `ServerManager:PredictiveModel:ExeName` | `Query` |

All three are launched with an empty argument string. The five-second gap is implemented with blocking `Task.Delay(...).Wait()` on the calling thread; the `ServerLauncher.startUpDelay` constructor option is not used here.

## Child-process implementation

Each `ServerLauncher` schedules fire-and-forget work with `Task.Run`:

1. Acquire a static lock shared by all launcher instances for the start section.
2. Set the **manager process's global** `Environment.CurrentDirectory` to the configured child working directory.
3. Build `ProcessStartInfo` with:
   - hidden/no-window intent;
   - `UseShellExecute = false`;
   - standard output redirected;
   - executable path formed as `<workingDirectory>\<exeName>`;
   - optional arguments; and
   - `WorkingDirectory = null`.
4. Start the process.
5. attach an `OutputDataReceived` handler and begin asynchronous line reads;
6. release the start lock;
7. block the background task in `WaitForExit()`;
8. clear the stored process reference when the child exits.

On an exception, the launcher attempts to kill and close the process but does not report the error. `Dispose` similarly kills and closes a currently stored process and suppresses failures.

## Reset and shutdown order

Both reset and application exit stop processes in reverse dependency order:

1. Predictive Model
2. Event
3. Telemetry

`Reset` immediately calls `StartServers` after disposal. Application exit additionally sets `NotifyIcon.Visible = false` and requests WPF shutdown.

## Configuration surface

### Configuration consumed directly

| Key | Purpose |
| --- | --- |
| `ServerManager:Telemetry:WorkingDirectory` | Directory containing the telemetry executable. |
| `ServerManager:Telemetry:ExeName` | Telemetry executable filename. |
| `ServerManager:Event:WorkingDirectory` | Directory containing the event executable. |
| `ServerManager:Event:ExeName` | Event executable filename. |
| `ServerManager:PredictiveModel:WorkingDirectory` | Directory containing the predictive-model executable. |
| `ServerManager:PredictiveModel:ExeName` | Predictive-model executable filename. |

`ServerManager:ServerNames` and the base file's `Serilog:*` hierarchy are present but are not read or bound by the current Server Manager source.

### Configuration files copied but not loaded

Development and Production files define `AppSettings` values for command, query, telemetry, Redis, and market-data-feed endpoints. Because `App.OnStartup` loads only `appsettings.json`, these environment-specific values do not participate in the current configuration object.

The project file copies all three settings files to output and publish directories, but copy behavior alone does not load them.

## Project and deployment definition

- SDK: `Microsoft.NET.Sdk`
- Output type: Windows executable (`WinExe`)
- Target framework: `net10.0-windows7.0`
- Platform target: x64
- WPF: enabled
- Windows Forms: enabled
- Nullable reference types: enabled
- Product: `IFM Server Manager`
- Assembly: `IFMServerManager`
- Application icon: `Resources/AppIcon.ico`
- Project reference: `TomasAI.IFM.Shared`

Package dependencies are CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions Configuration/DI packages 10.0.10.

The project is included in `TomasAI.IFM.sln`. Checked-in publish options support both folder publication and ClickOnce; the ClickOnce profile includes install/update, manifest, shortcut, runtime, and signing-related settings.

## Operational characteristics and current limitations

- **Windows-only UI.** WPF, Windows Forms, tray icons, and the Windows-specific target require a Windows desktop environment.
- **Only standard output is captured.** Standard error is not redirected, so child errors written exclusively to stderr do not reach the console grid.
- **Launch failures are silent.** Empty paths, missing executables, access errors, and process-start errors are swallowed by `ServerLauncher`.
- **Configuration validation is absent.** Missing settings fall back to empty strings and are passed to the launcher.
- **Environment-specific settings are unused.** Only base `appsettings.json` is loaded.
- **The manager's working directory is mutated globally.** Each launch assigns `Environment.CurrentDirectory`; after startup it reflects the last launched server's directory and can affect unrelated relative-path operations.
- **The child working directory is implicit.** `ProcessStartInfo.WorkingDirectory` is set to `null`, relying on inherited/global process state.
- **Lifecycle work is fire-and-forget.** Launcher tasks are not retained, awaited, or cancellable.
- **A start/dispose race is possible.** Disposal can observe `_process == null` while a background launch has not yet assigned it, allowing a process to start after a stop/reset request.
- **Termination is abrupt.** `Process.Kill()` is used without a graceful shutdown request and without explicitly killing a descendant process tree.
- **Exceptions are suppressed.** Both launch and disposal catch blocks discard diagnostic details.
- **Readiness is time-based.** A fixed five-second UI-thread wait separates Telemetry and Event; no health check establishes readiness, and Event/Predictive Model have no separation.
- **Reset blocks the UI during waits.** Reset runs from a tray callback and `StartServers` performs the synchronous five-second delay.
- **Tray exit is indirect.** No explicit Exit menu item exists; closing the console window is the practical normal exit path.
- **The tray icon is hidden but not disposed explicitly.** Cleanup sets `Visible = false` but does not call `NotifyIcon.Dispose()`.
- **Two observable wrapper models are unused.** `ConsoleVisibilityModel` and `ConsoleWindowStateModel` are not registered or referenced by the active UI path.
- **View methods are unused/incomplete.** `MainWindow.AddServerLog` writes to `Console`, while `Clear` is empty; the context uses the view model instead.
- **Predictive output is labeled Query.** This is the current `ServerLogType` mapping and may be intentional, but no dedicated Predictive Model label is used.

## Safe extension points

1. Bind launch settings to validated options and fail visibly when a path or executable is invalid.
2. Load environment-specific configuration intentionally if those files are meant to affect Server Manager behavior.
3. Set `ProcessStartInfo.WorkingDirectory` directly without changing `Environment.CurrentDirectory`.
4. Redirect and label standard error in addition to standard output.
5. Retain launcher tasks and add cancellation-aware, race-safe start/stop state.
6. Prefer graceful child shutdown with bounded fallback termination and explicit process-tree policy.
7. Replace fixed blocking waits with asynchronous readiness checks.
8. Add an explicit tray Exit command and dispose the notification icon.
9. Surface launch/exit diagnostics in the status collection or structured logging.

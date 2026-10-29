# IFM development launch profiles

## API + eGui with Rust MarketData

Select **IFM: API + eGui Rust UI + Rust MarketData (Development)** and press `F5`.
The preparation tasks stop the prior repository session, incrementally build the
Rust market-data DLL/Host and eGui, build the Debug API/Server Manager/Scheduler,
and prepare a separate Manager configuration. That Manager starts the native
Host, API, eGui and Scheduler with the existing Development client profile.

The native Host package is `.artifacts/rust-marketdata-package/Debug`; the
Manager configuration is `.artifacts/rust-marketdata-development/Manager`.
The API uses its normal `bin/Debug/net10.0` output. Dataset resources are owned
inside the native Host's Rust implementation. The regular launch choices keep
their current backend selection. Native Release acceptance preparation retains
its existing defaults.

The stop preflight includes repository MarketData Hosts and dataset workers so
switching profiles cannot leave the previous backend responding on the same
NATS subjects. Preparing the configuration alone does not start processes.

## Existing development profiles

Open **Run and Debug** in VS Code and select **IFM: API + UI (Development)**.
Press `F5` to:

1. stop and verify any prior IFM process loading API/UI repository Debug outputs;
2. build the API Server and UI.Net Debug binaries;
3. start the API Server with the Development configuration;
4. wait for `http://localhost:22543/health/ready` to return success; and
5. start UI.Net under the debugger with its main window maximized.

Stopping either member of the compound session stops both debug sessions. When the API debug session ends, its
`postDebugTask` runs the same stop-and-verify operation so a detached `dotnet` host cannot retain API/UI output files.
The dropdown also contains individual API and UI profiles. The individual UI profile expects the Development API to
be running already.

If a Development process is left running outside an active debug session, run
**Tasks: Run Task > IFM: Stop API + UI (Development)**. The task identifies both application-host executables and
`dotnet` processes by the exact loaded API/UI assembly path. It only stops processes whose validated application
path is beneath this repository's `bin\Debug` directories; published, deployed, infrastructure, IDE, compiler, and
test processes are ignored.

If the managed Development Server Manager is already active, the debugger preflight first requests its normal
control-pipe shutdown and verifies that its owned API/UI children exited before building.

These profiles run from repository Debug output and do not publish to or launch from the Production deployment
directories.

Build and publish tasks invoke scripts/Build/Invoke-DotNet.ps1. It fills missing
APPDATA and ProgramFiles(x86) values from Windows known folders before running
dotnet, avoiding NuGet restore failures such as Value cannot be null (path1)
when VS Code inherits an incomplete environment. Existing values are preserved;
no user or machine environment settings are changed. The Rust UI build uses the
same initializer before generating its catalog and building the managed bridge.

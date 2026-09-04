# IFM development launch profiles

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

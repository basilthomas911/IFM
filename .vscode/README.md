# IFM development launch profiles

Open **Run and Debug** in VS Code and select **IFM: API + UI (Development)**.
Press `F5` to:

1. build the API Server and UI.Net Debug binaries;
2. start the API Server with the Development configuration;
3. wait for `http://localhost:22543/health/ready` to return success; and
4. start UI.Net under the debugger with its main window maximized.

Stopping either member of the compound session stops both debug sessions. The dropdown also contains individual API
and UI profiles. The individual UI profile expects the Development API to be running already.

If a Development process is left running outside an active debug session, run
**Tasks: Run Task > IFM: Stop API + UI (Development)**. The task only stops executables whose resolved paths are
beneath this repository's `bin\Debug` directories; published or otherwise deployed processes are ignored.

These profiles run from repository Debug output and do not publish to or launch from the Production deployment
directories.

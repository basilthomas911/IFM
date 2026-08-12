# UI system-test results

Runtime artifacts are written beneath `Runs/<UTC timestamp>-<configuration>/`. Each run contains a machine-readable result, human-readable summary, process and network evidence, application/server logs, screenshots, and UI Automation tree dumps.

Raw run directories are ignored by Git because they can be large and environment-specific. Accepted gate summaries that must be retained with the code should be copied into `TomasAI.IFM.UI.Net/Docs/TestResults/` and linked from the WinForms UI system-test specification.

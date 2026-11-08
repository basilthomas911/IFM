# Rust build targets

Rust build output resides under `E:\IFM\RustBuildCache`, preserving the project
path relative to the repository. Each project's `target` directory is a Windows
junction to that location. Cargo and existing packaging paths continue to use
the project-local `target` path.

The Rust packaging scripts call `Initialize-RustBuildTarget.ps1`. For a fresh
checkout, or after removing a target junction, initialize it before invoking
Cargo directly:

```powershell
& C:\repos\IFM\scripts\Development\Initialize-RustBuildTarget.ps1 `
    -ProjectDirectory C:\repos\IFM\TomasAI.IFM.UI.Rust.eGui
```

The initializer refuses to overwrite an existing real directory. Migrating an
existing cache requires copying it, verifying its contents, and replacing the
original with the expected junction.

All maintained Rust crates declare `debug = 2` and `strip = "none"` in their dev,
test and release profiles. Custom Development profiles also retain full debug
information. Packaging includes the matching PDB beside the binary. Release
optimization can still affect stepping and variable availability.

Verify a built Windows binary against its PDB and a project source file:

```powershell
python C:\repos\IFM\scripts\Development\Verify-RustDebugSymbols.py `
    C:\repos\IFM\TomasAI.IFM.UI.Rust.eGui\target\x86_64-pc-windows-msvc\development\ifm-ui-egui.exe `
    C:\repos\IFM\TomasAI.IFM.UI.Rust.eGui\target\x86_64-pc-windows-msvc\development\ifm_ui_egui.pdb `
    C:\repos\IFM\TomasAI.IFM.UI.Rust.eGui\src\main.rs
```

The verifier checks the PE/PDB identity, type records and project source-file
entry. Migration and build evidence is saved in `.artifacts/rust-target-migration`.
Original migrated caches are retained on E: as `target.migration-backup` siblings;
they are recovery copies and are not used by Cargo.

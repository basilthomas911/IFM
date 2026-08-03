# Azure Storage Implementation Details

## Purpose

`TomasAI.IFM.Framework.Storage.Azure` is a focused .NET 10 adapter that uploads configured database-backup files to Azure Blob Storage. It references `TomasAI.IFM.Shared` and `TomasAI.IFM.Domain.SystemAdmin.Shared` and uses Azure.Storage.Blobs 12.29.1.

All production source files are stored in the project root.

## Root-to-Leaf Directory Inventory

Every current directory leaf is listed below relative to the project root. Each path includes its intermediate parents. `bin/` and `obj/` are generated build trees.

- `Docs/`
- `bin/Debug/net10.0/`
- `bin/Debug/net8.0/`
- `bin/Release/net10.0/`
- `obj/Debug/net10.0/ref/`
- `obj/Debug/net10.0/refint/`
- `obj/Debug/net8.0/ref/`
- `obj/Debug/net8.0/refint/`
- `obj/Release/net10.0/ref/`
- `obj/Release/net10.0/refint/`

## Root Files

| File | Responsibility |
| --- | --- |
| `IAzureStorage.cs` | Defines backup upload and configured-file lookup operations. |
| `IAzureStorageFile.cs` | Defines the name, container, backup type, source, and destination fields for one upload. |
| `IAzureStorageOptions.cs` | Defines connection and backup-file configuration. |
| `AzureStorageFile.cs` | Mutable configuration model for one file mapping. |
| `AzureStorageOptions.cs` | Mutable options collection and exact name/type lookup. |
| `AzureStorage.cs` | Azure Blob client orchestration and file upload. |
| `TomasAI.IFM.Framework.Storage.Azure.csproj` | Defines the target framework, package, and project references. |

## Configuration Model

`AzureStorageOptions` contains an Azure Storage connection string and a collection of `AzureStorageFile` entries. Each entry provides:

- logical database `Name`;
- target blob `Container`;
- string `BackupType`;
- local file `Source`; and
- blob-path `Destination`.

`GetStorageFile(name, backupType)` performs an exact, case-sensitive match and returns the first entry or null. The public `AzureStorage.GetStorageFile` overload converts `DatabaseBackupType` to a lowercase string before lookup.

## Upload Flow

`AzureStorage.UploadFileAsync` performs these steps:

1. Creates a `BlobServiceClient` from the configured connection string.
2. Selects the configured file by database name and backup-type string.
3. Gets a container client and then a blob client for the destination path.
4. Optionally reports upload start through the asynchronous progress callback.
5. Opens the source with read-only `FileStream` access.
6. Calls `UploadAsync(stream, overwrite: true)`.
7. Optionally reports completion.

The component does not create containers and does not check their existence before upload. The upload replaces an existing blob at the configured destination.

## Errors and Operational Constraints

The upload method catches all exceptions and tries to report the failure through `progressFunc`. Because the catch path invokes the callback without a null check, a failed upload with no callback can surface a `NullReferenceException` instead of the original failure. A missing configuration entry can likewise be dereferenced before upload and then enter this catch path.

Until error handling is revised, callers should always provide a progress callback and separately log failures. Future implementations should validate options and source files up front, use a cancellation token, preserve the original exception, and decide explicitly whether failures are returned or thrown.

Storage connection strings are secrets. Load them from a secret provider or environment-specific configuration, never from committed production settings. Progress messages include local and blob paths, so logs must be protected appropriately.

## Threading and Lifetime

The adapter holds only the injected options and creates Azure clients per upload call. It opens and disposes the source stream inside the operation. There is no retry, concurrency limit, progress-by-byte reporting, cancellation, download, delete, or list operation in this project; Azure SDK defaults govern transport retries.

## Build and Related Tests

```powershell
dotnet build TomasAI.IFM.Framework.Storage.Azure/TomasAI.IFM.Framework.Storage.Azure.csproj --configuration Debug
```

Configuration binding is exercised by the storage unit-test project. The real upload test lives in the integration-test project and is skipped unless an Azure Storage environment is intentionally supplied.

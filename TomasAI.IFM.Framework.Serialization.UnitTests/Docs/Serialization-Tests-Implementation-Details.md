# Serialization Unit Tests Implementation Details

## Purpose

`TomasAI.IFM.Framework.Serialization.UnitTests` verifies the observable behavior of the serialization project. It targets .NET 10, uses xUnit, and references both `TomasAI.IFM.Framework.Serialization` and `TomasAI.IFM.Shared`.

All test source files are stored directly in the project root; there are no test-source subfolders.

## Directory Structure

The following inventory lists every directory leaf from the project root. Each path includes all of its intermediate parent folders.

| Leaf path | Purpose |
| --- | --- |
| `Docs/` | Maintained test implementation documentation. |
| `bin/Debug/net10.0/cs/` | Czech localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/de/` | German localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/es/` | Spanish localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/fr/` | French localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/it/` | Italian localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/ja/` | Japanese localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/ko/` | Korean localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/pl/` | Polish localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/pt-BR/` | Brazilian Portuguese localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/ru/` | Russian localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/runtimes/win/lib/net10.0/` | Windows-specific .NET 10 Debug runtime assets. |
| `bin/Debug/net10.0/runtimes/win/lib/net8.0/` | Windows-specific .NET 8-compatible Debug runtime assets. |
| `bin/Debug/net10.0/tr/` | Turkish localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/zh-Hans/` | Simplified Chinese localized .NET 10 Debug test-host resources. |
| `bin/Debug/net10.0/zh-Hant/` | Traditional Chinese localized .NET 10 Debug test-host resources. |
| `bin/Debug/net8.0/cs/` | Czech localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/de/` | German localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/es/` | Spanish localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/fr/` | French localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/it/` | Italian localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/ja/` | Japanese localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/ko/` | Korean localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/pl/` | Polish localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/pt-BR/` | Brazilian Portuguese localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/ru/` | Russian localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/runtimes/win/lib/net8.0/` | Windows-specific legacy .NET 8 Debug runtime assets. |
| `bin/Debug/net8.0/tr/` | Turkish localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/zh-Hans/` | Simplified Chinese localized legacy .NET 8 Debug test-host resources. |
| `bin/Debug/net8.0/zh-Hant/` | Traditional Chinese localized legacy .NET 8 Debug test-host resources. |
| `bin/Release/net10.0/cs/` | Czech localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/de/` | German localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/es/` | Spanish localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/fr/` | French localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/it/` | Italian localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/ja/` | Japanese localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/ko/` | Korean localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/pl/` | Polish localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/pt-BR/` | Brazilian Portuguese localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/ru/` | Russian localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/runtimes/win/lib/net10.0/` | Windows-specific .NET 10 Release runtime assets. |
| `bin/Release/net10.0/tr/` | Turkish localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/zh-Hans/` | Simplified Chinese localized .NET 10 Release test-host resources. |
| `bin/Release/net10.0/zh-Hant/` | Traditional Chinese localized .NET 10 Release test-host resources. |
| `obj/Debug/net10.0/ref/` | .NET 10 Debug reference assemblies. |
| `obj/Debug/net10.0/refint/` | .NET 10 Debug intermediate reference assemblies. |
| `obj/Debug/net8.0/ref/` | Legacy .NET 8 Debug reference assemblies. |
| `obj/Debug/net8.0/refint/` | Legacy .NET 8 Debug intermediate reference assemblies. |
| `obj/Release/net10.0/ref/` | .NET 10 Release reference assemblies. |
| `obj/Release/net10.0/refint/` | .NET 10 Release intermediate reference assemblies. |
| `TestResults/41b2b639-4f0c-4bd9-b22f-421972c69c55/` | Existing generated test-result run directory. |

`bin/`, `obj/`, and `TestResults/` are generated trees. Their current leaves are included for a complete on-disk inventory and can change when the SDK, test host, build configuration, or test run changes.

## Root Files

| File | Responsibility |
| --- | --- |
| `MessagePackBinarySerializerTests.cs` | Exercises null handling, round trips, collections, contracts, and malformed MessagePack input. |
| `SampleData.cs` | Supplies MessagePack-annotated command and identifier types used in round-trip tests. |
| `TomasAI.IFM.Framework.Serialization.UnitTests.csproj` | Defines test dependencies and project references. |

## Test Infrastructure

The project is marked as non-packable and as a test project. Its test packages are:

- `Microsoft.NET.Test.Sdk` 18.8.1
- `xunit` 2.9.3
- `xunit.runner.visualstudio` 3.1.5
- `coverlet.collector` 10.0.1

The project adds `Xunit` as a global using. The runner and coverage collector are private assets and do not flow to projects that reference this test assembly.

## MessagePack Test Coverage

`MessagePackBinarySerializerTests` constructs the concrete serializer directly and verifies:

- typed and object-typed null values serialize to `null`;
- null and empty byte arrays deserialize to the default value for reference and value types;
- null and empty byte arrays deserialize to a null `byte[]`;
- primitive values survive a round trip;
- an unannotated POCO works through the contractless resolver;
- an annotated generic `TestCommand<TestId>` survives a round trip;
- lists of POCOs and string-to-integer dictionaries survive round trips;
- malformed bytes cause an exception rather than being silently accepted.

The nested `SimplePoco` and `ChildPoco` types provide contractless object graphs. `Animal` and `Dog` describe a polymorphic shape but are not currently used by a test, so polymorphism is not verified by the suite.

## Sample Contracts

`SampleData.cs` contains serialization-specific test contracts that also implement shared actor abstractions:

- `TestCommand<TActorId>` implements `ICommand<TActorId>` and has MessagePack keys 0 through 6 for actor identity, message identity, timestamp, command name, aggregate version, aggregate type, and payload.
- Its `[SerializationConstructor]` defines the constructor MessagePack uses to reconstruct the command.
- `TestId` is a readonly record struct with a keyed `DateOnly` value and a stable `yyyy-MM-dd` text format.

These types validate explicit MessagePack contracts in addition to the contractless POCO scenario.

## Current Coverage Gaps

The suite does not currently verify:

- `NewtonSoftJsonSerializer` output, deserialization, null behavior, or errors;
- `SystemTextJsonSerializer` camel casing, case-insensitive reads, null behavior, or errors;
- JSON content-type and `DataFormat` metadata;
- MessagePack wire compatibility across model or option changes;
- polymorphic binary serialization;
- concurrency or large-payload behavior.

Tests for these behaviors should be added when they become compatibility requirements. Assertions should target public behavior rather than internal implementation details.

## Running the Tests

From the repository root:

```powershell
dotnet test TomasAI.IFM.Framework.Serialization.UnitTests/TomasAI.IFM.Framework.Serialization.UnitTests.csproj --configuration Debug
```

To collect cross-platform coverage when the collector is available:

```powershell
dotnet test TomasAI.IFM.Framework.Serialization.UnitTests/TomasAI.IFM.Framework.Serialization.UnitTests.csproj --configuration Debug --collect:"XPlat Code Coverage"
```

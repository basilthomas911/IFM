# Serialization Implementation Details

## Purpose

`TomasAI.IFM.Framework.Serialization` provides small, synchronous abstractions and concrete implementations for binary and JSON serialization. It targets .NET 10 and has no project references. Its external dependencies are MessagePack 3.1.8 and Newtonsoft.Json 13.0.4.

All production source files are stored directly in the project root; there are no source-code subfolders.

## Directory Structure

The following inventory lists every directory leaf from the project root. Each path includes all of its intermediate parent folders.

| Leaf path | Purpose |
| --- | --- |
| `Docs/` | Maintained implementation documentation. |
| `bin/Debug/net10.0/` | Current .NET 10 Debug build output. |
| `bin/Debug/net8.0/` | Legacy .NET 8 Debug build output retained in the working tree. |
| `bin/Release/net10.0/` | Current .NET 10 Release build output. |
| `obj/Debug/net10.0/ref/` | .NET 10 Debug reference assemblies. |
| `obj/Debug/net10.0/refint/` | .NET 10 Debug intermediate reference assemblies. |
| `obj/Debug/net8.0/ref/` | Legacy .NET 8 Debug reference assemblies. |
| `obj/Debug/net8.0/refint/` | Legacy .NET 8 Debug intermediate reference assemblies. |
| `obj/Release/net10.0/ref/` | .NET 10 Release reference assemblies. |
| `obj/Release/net10.0/refint/` | .NET 10 Release intermediate reference assemblies. |

`bin/` and `obj/` are generated build trees. They are documented to make the current on-disk hierarchy complete, but application code must not depend on their contents.

## Root Files

| File | Responsibility |
| --- | --- |
| `IBinarySerializer.cs` | Defines generic binary serialization and deserialization. |
| `IJsonSerializer.cs` | Defines generic and runtime-type JSON operations plus content metadata. |
| `MessagePackBinarySerializer.cs` | Implements binary serialization with MessagePack and LZ4 compression. |
| `NewtonSoftJsonSerializer.cs` | Implements JSON serialization with Newtonsoft.Json defaults. |
| `SystemTextJsonSerializer.cs` | Implements JSON serialization with configured `System.Text.Json` options. |
| `SerializerExtensions.cs` | Reserved internal extension point; currently contains no methods. |
| `TomasAI.IFM.Framework.Serialization.csproj` | Defines the .NET 10 project and package dependencies. |

## Public Contracts

### `IBinarySerializer`

The binary contract exposes two generic operations:

- `Serialize<TData>(TData data)` returns a nullable byte array.
- `Deserialize<TData>(byte[] data)` returns a nullable/default `TData`.

The implementation treats a null input as an absent payload rather than as a serialized null value.

### `IJsonSerializer`

The JSON contract exposes:

- serialization from `object` to `string`;
- generic deserialization from `string` to `T`;
- runtime-type deserialization from `string` and `Type` to `object`;
- `SupportedContentTypes`, mutable `ContentType`, and `DataFormat` metadata.

`DataFormat` currently declares `Json`, `Xml`, and `None`. This project only implements JSON; the XML member is an extensibility value, not evidence of an XML serializer.

## MessagePack Binary Implementation

`MessagePackBinarySerializer` uses a single static option set built from `MessagePackSerializerOptions.Standard`:

- `ContractlessStandardResolver.Instance` permits ordinary public-property objects to be serialized without MessagePack attributes.
- `MessagePackCompression.Lz4BlockArray` compresses serialized payloads.

Serialization returns `null` when the input value is null. Deserialization returns `default(TData)` when the input byte array is null or empty, despite the non-nullable array parameter in the interface. For non-empty data, the byte array is wrapped in a `ReadOnlySequence<byte>` and passed to MessagePack.

Malformed payloads and unsupported types are not caught or translated; MessagePack exceptions propagate to the caller. Producers and consumers must use compatible MessagePack options. In particular, the LZ4 block-array setting is part of the wire representation, while contractless property-name or shape changes can break compatibility with previously stored or transmitted payloads.

## JSON Implementations

Both JSON serializers report `DataFormat.Json`, default `ContentType` to `application/json`, and advertise these media types:

- `application/json`
- `text/json`
- `text/x-json`
- `text/javascript`
- `*+json`

Changing `ContentType` changes metadata only; it does not alter serialization behavior or validate the selected value.

### `NewtonSoftJsonSerializer`

- Serializes with `JsonConvert.SerializeObject` and `Formatting.None`.
- Uses Newtonsoft.Json's default contract and naming behavior.
- Supports both generic and runtime-`Type` deserialization.
- Does not install custom `JsonSerializerSettings`.

### `SystemTextJsonSerializer`

- Uses a shared `JsonSerializerOptions` instance.
- Writes camel-case property names.
- Matches property names case-insensitively while reading.
- Produces compact JSON because indentation is disabled.
- Supports both generic and runtime-`Type` deserialization.

The two serializers do not necessarily produce identical JSON. The System.Text.Json implementation applies camel casing explicitly, whereas the Newtonsoft implementation retains its default property naming policy.

## Nulls and Errors

The JSON implementations use the null-forgiving operator on deserialization results to satisfy the interface's non-null return declarations. A JSON `null` payload can therefore produce a runtime null even though the signature is non-nullable. Parsing, conversion, and contract errors are allowed to propagate from the underlying library.

Callers should validate untrusted input, enforce appropriate payload-size limits at system boundaries, and handle library-specific serialization exceptions where recovery is possible.

## Extension and Maintenance Notes

- `SerializerExtensions` is currently an empty internal class and has no runtime effect.
- There is no non-generic or typeless binary API.
- There are no streaming, asynchronous, or cancellation-aware operations.
- Adding a serializer requires implementing the relevant interface and registering it in the consuming application's dependency-injection composition root; this project contains no registration code.
- Changes to serializer options should be treated as protocol changes when serialized data crosses process boundaries or is persisted.

## Verification

Build the project from the repository root with:

```powershell
dotnet build TomasAI.IFM.Framework.Serialization/TomasAI.IFM.Framework.Serialization.csproj --configuration Debug
```

The companion test project and its coverage are documented in `TomasAI.IFM.Framework.Serialization.UnitTests/Docs/Serialization-Tests-Implementation-Details.md`.

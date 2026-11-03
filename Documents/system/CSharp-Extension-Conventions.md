# C# extension conventions

First-party IFM projects use C# 14 extension blocks, supported by the .NET 10 SDK.
The bundled IBKR source under `TWS API/` retains its upstream syntax.

Declare extension members inside a non-generic, top-level static class:

```csharp
public static class OrderExtensions
{
    extension(Order order)
    {
        public bool IsOpen() => order.Status == OrderStatus.Open;
    }
}
```

Group adjacent methods sharing the receiver type, name and generic constraints.
Keep receiver names descriptive and preserve existing names during migrations, since
`nameof`, named static calls and exception parameter names can depend on them.
Keep method accessibility, attributes, parameter defaults and behavior unchanged.
Use extension properties or static extensions only when the API actually needs them;
converting an existing method into a property changes its callers and contract.

Receiver type parameters and their constraints belong on the block. Method-only
type parameters remain on the method. Preserve the original combined type parameter
order: the compiler emits block parameters before method parameters. Where necessary,
move an ordered prefix of parameters onto the block to retain explicit generic calls.
Document receiver parameters and block type parameters on the block; leave method
summaries, remaining parameter documentation and return documentation on the method.

Source-generated `LoggerMessage` partial methods stay as ordinary static methods on
the containing partial class. Expose an extension-block wrapper and forward all
arguments, including the original caller member name. Set `EventName` explicitly
when renaming the generated method so structured logging keeps its event identity.

Existing calls such as `order.IsOpen()` and `OrderExtensions.IsOpen(order)` remain
supported. Extension methods are still compiled as static implementation methods.

Run the repository convention audit from the repository root:

```powershell
dotnet run --project scripts/Architecture/CSharp14Extensions -- .
dotnet run --project scripts/Architecture/CSharp14Extensions -- . --self-test
```

The audit uses the SDK's Roslyn parser with and without framework symbols and checks
tracked and non-ignored untracked C# source. It excludes the bundled SDK. It does not
require downloading an analyzer package.

Reference: [C# extension members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods).

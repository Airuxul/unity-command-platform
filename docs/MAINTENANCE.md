# Maintenance guide

## Layout

| Path | Role |
|------|------|
| `unity-cmd/` | Node CLI — profiles, catalog cache, integration tests |
| `com.air.unity-connector/` | Unity UPM — HTTP hosts, commands, job state |
| `docs/` | Architecture, agents, this file |

## Connector framework

| Layer | Key types |
|-------|-----------|
| Commands | `CliCommand` / `CliCommand<T>`, `InvokeDescriptor`, `CliCommandDiscovery` |
| HTTP | `ConnectorRequestDispatcher`, `InvokePipeline`, `HttpListenerHost` |
| Job state | `JobStateCore`, `InvokeJobRecord`, `EditorJobStateManager` (Editor), `RuntimeJobStateManager` (play/player) |
| Execution | `InvokeExecutor`, `InvokePipeline` |

## Builtin command names (C#)

Rename or add built-in commands in **`com.air.unity-connector/Runtime/Commands/CommandNames.cs`**. Reference `CommandNames.*` from command `Descriptor` and tests under `com.air.unity-connector/Tests/`.

### Command shape

Each command is an **instantiable class** (not abstract `CliCommand` / `CliCommand<T>`):

| Piece | Shape |
|-------|--------|
| Base | `CliCommand` or `CliCommand<TParams>` |
| Metadata | `InvokeDescriptor` / `DeferredInvokeDescriptor<TParams>` |
| Entry | `Run()` or `Run(TParams)` |

**Deferred commands:** call `MarkRunning()` then `CompleteSuccess` / `CompleteFail` when done. Editor compile/play policies use `DeferredInvokeDescriptor` + `InvokeCompletionCatalog` + `Editor/Completion/*`.

Example:

```csharp
public sealed class MyDeferredCommand : CliCommand<MyParams>
{
    public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<MyParams>(
        "my.deferred", CommandHostScope.Runtime, "Background work", defaultTimeoutMs: 15000);

    public override void Run(MyParams p)
    {
        MarkRunning();
        _ = System.Threading.Tasks.Task.Run(() => CompleteSuccess(InvokeResult.Ok("done")));
    }
}
```

Discovery (`CliCommandDiscovery`): scans assemblies, skips abstract types, builds `IInvokeHandler` per concrete command class.

Design notes: `com.air.unity-connector/Runtime/Connector/Command/DESIGN.md`.

### Command parameters

- Define a `*Params` class per command (`Editor/Params/`, `Runtime/Params/`).
- `[CliParam]` on properties; `CliParamBinder` binds JSON from HTTP/CLI.

## Bump connector build

When changing HTTP protocol or catalog contract:

1. Implement change in `com.air.unity-connector`
2. Increment `com.air.unity-connector/Runtime/ConnectorBuild.cs` → `Id`
3. Set `unity-cmd/src/constants.js` → `MIN_CONNECTOR_BUILD` to the same value
4. Update `expectConnectorBuild` in integration JSON scenarios if present
5. Run `npm run verify` from `unity-cmd/` (includes `doc:check` for version + build sync)

## Tests

From `unity-cmd/`:

```bash
npm run verify          # unit + doc:check
npm run test:integration   # needs Unity + profiles
```

Unity EditMode: `UnityCliConnector.Tests.Editor` assembly in consuming project.

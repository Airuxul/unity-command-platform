# Maintenance guide

## Layout

| Path | Role |
|------|------|
| `unity-cmd/` | Node CLI — profiles, catalog cache, integration tests |
| `unity-connector/` | Unity UPM — HTTP hosts, commands, command-status tracking |
| `docs/` | Architecture, agents, this file |

## Core framework (connector)

| Layer | Key types |
|-------|-----------|
| Commands | `CommandBase`, `ICommand` / `ICommand<T>`, `CommandDescriptor`, `CommandDiscovery` |
| HTTP | `ConnectorRequestDispatcher`, `CommandPipeline`, `ConnectorJson` |
| Command state | `CommandStateCore`, `CommandContext`, `ICommandNotifier`, `CommandStateManager` (Editor), `RuntimeCommandStateManager` (play/player) |
| Execution | `CommandPipeline`, `CommandExecutor` (single command lifecycle path) |

## Builtin command names (C#)

Rename or add built-in commands in **`unity-connector/Core/Commands/CommandNames.cs`**. Reference `CommandNames.*` from command `Descriptor` and tests under `unity-connector/Tests/`.

### Command shape

Each command is an **instantiable class**:

| Piece | Unified command shape |
|-------|------------------------|
| Marker | `CommandBase` + `ICommand` / `ICommand<TParams>` + `ICommandDescriptorProvider` |
| Metadata | `CommandDescriptor` / `DeferredCommandDescriptor<TParams>` |
| Methods | `Run()` or `Run(TParams)` |

**Deferred commands (recommended):** omit custom completion policy files when possible. In `Run(...)`, call `CompleteSuccess(result)` / `CompleteFail(error)`. For background work, call `MarkRunning()` then complete later.

**Editor policy deferred commands:** use explicit completion in descriptor — `CompletionCompilation`, `CompletionEnterPlay`, `CompletionExitPlay`.

**Special case:** `refresh` uses `DeferredCommandDescriptor` with `CompletionCompilation`; `compile=false` can complete immediately while `compile=true` defers completion until policy triggers.

Example (deferred command):

```csharp
public class MyDeferredCommand : CommandBase, ICommand<MyParams>, ICommandDescriptorProvider
{
    public CommandDescriptor Descriptor { get; } = new DeferredCommandDescriptor<MyParams>(
        "my.deferred", CommandScope.Runtime, "Background work", defaultTimeoutMs: 15000);

    public void Run(MyParams p)
    {
        MarkRunning();
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            var value = DoWork(p);
            CompleteSuccess(new { value });
        });
    }
}
```

Discovery (`CommandDiscovery`): performs one lightweight instance creation to read `ICommandDescriptorProvider.Descriptor`, then routes by host via `FindForHost(name, hostKind)`. Actual command instances are created per execution.

Design guardrails are documented in `unity-connector/Core/Commands/DESIGN.md`.

### Command parameters

- Define a `*Params` class per command (`Editor/Builtin/BuiltinParams.cs`, `Core/Builtin/EchoParams.cs`).
- `[CliParam]` on properties; omit key → camelCase property name (`ToolName` → `toolName`).
- `Required`, `AlternateKeys`, `AllowedValues` for validation and help.

## Bump connector build

When changing HTTP protocol or catalog contract:

1. Implement change in `unity-connector`
2. Increment `unity-connector/Core/ConnectorBuild.cs` → `Id`
3. Set `unity-cmd/src/constants.js` → `MIN_CONNECTOR_BUILD` to the same value
4. Update `expectConnectorBuild` in integration JSON scenarios
5. Run `npm run verify` from `unity-cmd/` (includes `doc:check` for version + build sync)

## Tests

From `unity-cmd/`:

```bash
npm run verify          # unit + doc:check
npm run test:integration   # needs Unity + profiles
```

Unity EditMode: `UnityCliConnector.Tests.Editor` assembly in consuming project.

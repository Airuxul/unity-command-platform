# CLI command architecture (`com.air.unity-connector`)

## Layers

| Layer | Package / folder | Role |
|-------|------------------|------|
| GoF undo | `com.air.game-core` → `Air.GameCore.Command` | Gameplay `ICommand` / `CommandHistory` only |
| HTTP + invoke | `Runtime/Host`, `Http`, `Invoke`, `Job` | Ports, `HttpListenerHost`, `InvokeCatalog`, deferred job state |
| Connector HTTP | `Runtime/Connector/Http` | `ConnectorRequestDispatcher`, `ConnectorMainThreadScheduler`, `InvokePipeline` |
| CLI commands | `Runtime/Cli`, `Editor/Commands`, `Runtime/Commands` | `CliCommand` / `CliCommand<T>`, `CliCommandDiscovery` |

`CliCommand` does **not** inherit game-core `CommandBase`. Commands expose `InvokeDescriptor` and run via `InvokeExecutor` on the host main thread.

## Builtin command names

Edit [`Runtime/Commands/CommandNames.cs`](../../Commands/CommandNames.cs). Reference `CommandNames.*` from command `Descriptor` and tests.

## Adding a command

1. Subclass `CliCommand` or `CliCommand<TParams>` (non-abstract).
2. Implement `Descriptor` (`InvokeDescriptor` or `DeferredInvokeDescriptor<TParams>` with `CommandHostScope`).
3. Implement `Run()` / `Run(TParams)`; long work: `MarkRunning()` then `CompleteSuccess` / `CompleteFail` on `InvokeContext`.
4. Editor-only deferred policies: `Editor/Completion/*` + `InvokeCompletionCatalog`.

Discovery: `CliCommandDiscovery` skips abstract/open-generic types; builds `IInvokeHandler` wrappers that create a fresh command instance per execution.

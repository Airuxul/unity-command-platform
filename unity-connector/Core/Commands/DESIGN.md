# Core Commands Design

This directory defines the command contract used by connector hosts.

## Rules

1. Commands must be class-based and instantiable.
2. Commands must inherit `CommandBase`.
3. Commands expose metadata through `ICommandDescriptorProvider.Descriptor`.
4. Commands implement exactly one execution entry:
   - `void Run()` for no-parameter commands, or
   - `void Run(TParams)` for typed commands.
5. Commands never return `CommandResult`.
6. Commands signal lifecycle by callbacks:
   - immediate: `CompleteSuccess(data)` / `CompleteFail(error)`
   - deferred: `MarkRunning()` now, then complete later.

## Runtime model

- Framework creates a fresh command instance per execution.
- Framework binds an `ICommandRuntime` into `CommandBase` before `Run(...)`.
- `CommandPipeline` always allocates a `command_id` and decides `200` vs `202` by whether command completed during `Run(...)`.

## Scope and host

- Scope is metadata (`Editor`, `Runtime`, `Any`) on descriptor.
- Host compatibility is resolved by `CommandAvailability`.
- Routing remains metadata-driven, not name-prefix or runtime reflection on `Run` signatures.

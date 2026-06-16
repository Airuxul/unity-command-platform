# Maintenance guide

## Layout

| Path | Role |
|------|------|
| `ucp-cli/` | Node — CLI, host, protocol, tests |
| `com.air.ucp-agent/` | Unity UPM — agent, commands |
| `docs/` | Architecture, agents, maintenance |

## Add an Editor command

1. Add constant to `com.air.ucp-agent/Runtime/Commands/CommandNames.cs`.
2. Create `Editor/Commands/{Name}Command.cs` in namespace `Air.UcpAgent.Editor.Commands`.
3. Inherit `CliCommand` or `CliCommand<TParams>` with `InvokeDescriptor`.
4. Recompile Unity — `CliCommandDiscovery` picks it up automatically.
5. Test: `ucp-cli run <name> [--args '{}']`.

Deferred example (`compile`, `play`, `stop`): use `DeferredInvokeDescriptor`, `MarkRunning()`, complete via `EditorJobStateManager`.

## Command shape (C#)

| Piece | Location |
|-------|----------|
| Name constants | `Runtime/Commands/CommandNames.cs` |
| Implementation | `Editor/Commands/*Command.cs` |
| FileQueue bridge | `Editor/Bridge/UcpCliCommandHandler.cs` |
| Protocol DTOs | `Runtime/Protocol/` |

## Node development

```powershell
cd ucp-cli
npm run verify                    # build + lint + unit + file-queue e2e
$env:UCP_EDITOR_INTEGRATION="1"
npm run test:integration:editor   # needs Unity
```

## Bump agent version

When changing session/command JSON shape:

1. Update `Runtime/Protocol/` and `ucp-cli/src/shared/` together.
2. Bump `com.air.ucp-agent/package.json` version.
3. Re-run Unity + integration tests.

## Environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `UCP_ROOT` | `~/.ucp` | Data root |
| `UCP_HOST_PORT` | `6610` | Host HTTP |
| `UCP_EDITOR_INTEGRATION` | unset | Enable live tests |

## Troubleshooting

| Symptom | Check |
|---------|--------|
| `no_ready_session` | Unity open, agent compiled, `~/.ucp/sessions/*.json` exists |
| Host won't start | Kill stale `node …/ucp-host.js` from old paths; delete `host.lock` |
| Command missing | Unity recompile; session `capabilities` list |
| BOM parse error | Agent writes UTF-8 without BOM; CLI strips BOM on read |

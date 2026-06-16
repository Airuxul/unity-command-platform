# Agent Changelog — `packages/ucp`

## 2026-06-16 — UCP migration & doc refresh

- Renamed CLI binary `uctl` → **`ucp-cli`**; repo path **`packages/ucp`**.
- Removed `com.air.unity-connector`, `unity-cmd`, Editor HTTP stack.
- Stack: `ucp-cli` → `ucp-host` → FileQueue → `com.air.ucp-agent`.
- Data root: `~/.ucp/` (replaces `~/.unity-cmd/`).
- Commands: single `CliCommand` track; `state` returns `agent_state`.
- Rewrote `docs/*`, `README*`, `CONVENTIONS.md`, `TODO*.md` for UCP.

## 2026-06-04 and earlier

Entries before 2026-06-16 refer to the **legacy** `unity-cmd` + `com.air.unity-connector` HTTP architecture and are kept for historical context only.

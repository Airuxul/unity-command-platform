# Documentation Governance — `packages/ucp`

**Last Updated:** 2026-06-16 · **Scope:** documentation workflow (English)

## Purpose

Define how user and agent documentation is structured in this Git submodule (`packages/ucp`).

## Document layout

| Track | Paths | Language |
|-------|--------|----------|
| User | `README.md`, `README.zh-CN.md` | EN + ZH |
| Conventions | `CONVENTIONS.md` | English |
| Agent | `docs/AGENTS.md`, `docs/DOC_GOVERNANCE.md`, `docs/CHANGELOG_AGENT.md` | English |
| Subproject | `ucp-cli/README.md`, `com.air.ucp-agent/README.md` | English |

## Legacy

Documents referencing `unity-cmd`, `unity-connector`, `~/.unity-cmd/`, or Editor HTTP `:6547` are **obsolete**. Do not extend them; update UCP docs listed in [docs/README.md](README.md).

## Update workflow

1. Read [AGENTS.md](AGENTS.md) and this file.
2. Keep `README.md` and `README.zh-CN.md` in sync for user-visible changes.
3. Update [CONVENTIONS.md](../CONVENTIONS.md) when naming or layout changes.
4. Append [CHANGELOG_AGENT.md](CHANGELOG_AGENT.md) for non-trivial agent doc work.
5. Align with [01_Architecture.md](../../../01_Architecture.md) for architectural claims.

## Command documentation source of truth

- **Names:** `com.air.ucp-agent/Runtime/Commands/CommandNames.cs`
- **Behavior:** `Editor/Commands/*Command.cs`
- **CLI usage:** `ucp-cli run <type>`

Do not maintain a separate markdown command catalog in this repo.

# Documentation Governance — `unity-command-platform`

**Last Updated:** 2026-11-06 · **Scope:** documentation workflow (English)

## Purpose

Define how user and agent documentation is structured in this repository.

## Document layout

| Track | Paths | Language |
|-------|--------|----------|
| User | `README.md`, `README.zh-CN.md`, `docs/GUIDE.zh-CN.md`, `docs/COMMAND-DESIGN.zh-CN.md` | EN + ZH; the walkthrough and the command-design guide are Chinese |
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
5. Keep [ARCHITECTURE.md](ARCHITECTURE.md) in step with the code; it is the architecture reference.

Each fact has one home. Do not copy it into a second doc:

| Fact | Home |
|------|------|
| What the product is, and how to install it | `README.md` / `README.zh-CN.md` |
| Walkthrough, sessions, everyday commands, adding a command in a game | `docs/GUIDE.zh-CN.md` |
| How to choose deferred completion, lane bypass, compiled scripts, and edit targets | `docs/COMMAND-DESIGN.zh-CN.md` |
| Reload, delivery, deadlines | `docs/RELIABILITY.zh-CN.md` |
| How to add a command inside this repo, tests | `docs/MAINTENANCE.md` |
| Names and layout | `CONVENTIONS.md` |
| What an agent caller must follow | `docs/AGENTS.md` |

Do not put a command-family table, a timeout number list, or the domain-reload procedure in the READMEs.

## Command documentation source of truth

- **Names:** the command descriptor. `CommandNames.cs` only when other C# mentions the name.
- **Behavior:** `Editor/Commands/*Command.cs`
- **CLI usage:** `ucp-cli help` (generated); `ucp-cli run <type>`

Do not maintain a separate markdown command catalog in this repo.

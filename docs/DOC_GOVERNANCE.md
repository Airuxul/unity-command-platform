# Documentation Governance — `unity-cli`

**Last Updated:** 2026-06-02 · **Owner:** package maintainers · **Scope:** documentation workflow (English)

## Purpose

Define how user and agent documentation is structured in **this** Git submodule, while linking standards in [AirUnityPackage](https://github.com/Airuxul/AirUnityPackage).

## Document layout

| Track | Paths | Language |
|-------|--------|----------|
| User | `README.md`, `README.zh-CN.md` | English + Chinese |
| Agent | `docs/AGENTS.md`, `docs/DOC_GOVERNANCE.md`, `docs/CHANGELOG_AGENT.md` | English |
| Subproject | `unity-cmd/`, `com.air.unity-connector/` README and `*/IMPLEMENTATION.md` | Per folder |

There is **no** `docs/unity-cmd-skill/` in this repo. The **unity-cmd** Cursor skill lives only in the meta repo: `.cursor/skills/unity-cmd/`.

## Cursor skills (meta repo)

| Skill | Location | Purpose |
|-------|----------|---------|
| `doc-read-index`, `doc-generate-update` | `AirUnityPackage/.cursor/skills/` | Package/meta documentation |
| `unity-cmd` | `AirUnityPackage/.cursor/skills/unity-cmd/` | Unity Editor/Player automation |

**Do not** copy doc skills or `unity-cmd` into this submodule.

## Update workflow

1. Read [AGENTS.md](AGENTS.md) and this file.
2. Use meta `doc-read-index` / `doc-generate-update` from the CustomPackages root for README/`docs/` maintenance.
3. Keep `README.md` and `README.zh-CN.md` in sync for user-visible edits.
4. Append [CHANGELOG_AGENT.md](CHANGELOG_AGENT.md) for non-trivial agent doc work.

## Cross-repo standards

See [AirUnityPackage DOC_GOVERNANCE](https://github.com/Airuxul/AirUnityPackage/blob/main/docs/DOC_GOVERNANCE.md), [ARCHITECTURE](https://github.com/Airuxul/AirUnityPackage/blob/main/.cursor/rules/PACKAGE_ARCHITECTURE.md), [C_SHARP_STANDARDS](https://github.com/Airuxul/AirUnityPackage/blob/main/.cursor/rules/C_SHARP_STANDARDS.md).

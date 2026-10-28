# Confirmed project context

Unity project at repository root (created 2026-10-26). This is an index of confirmed facts; verify current values in the authoritative files.

| Topic | Confirmed fact / source |
| --- | --- |
| Name | UnitySlotTower (folder, productName in `ProjectSettings/ProjectSettings.asset`); official title unconfirmed |
| Repository | https://github.com/acururc/UnitySlotTower.git (`main`) |
| Gameplay / targets | Undecided. Start with the `spec` skill; current state in `HANDOFF.md` |
| Unity | 6000.6.7f1 — `ProjectSettings/ProjectVersion.txt`; created from the bundled `com.unity.template.urp-blank-17.2.6` template |
| Rendering | URP 17.6.0 (3D) — package files; `Assets/Settings/PC_RPAsset.asset` / `Mobile_RPAsset.asset` |
| Input | Input System 1.20.1, `activeInputHandler: 1` (new only); `Assets/InputSystem_Actions.inputactions` |
| UI | Undecided; uGUI 2.6.0 and UI Toolkit module installed |
| Scene | `Assets/Scenes/SampleScene.unity` enabled in Build Settings (template) |
| Tests | Unity Test Framework 1.8.0 (builtin); no tests yet |
| Code | Automation helpers: `Assets/UnitySlotTower/Automation/` (Editor connection, dev-player screenshot). Game code location: `Assets/UnitySlotTower/` |
| Unity automation | MCP for Unity 10.2.0 package + pinned Python CLI/server in `.tools/unity-mcp` (Python 3.12); 127.0.0.1:8080; `UNITY_AUTOMATION.md`. Read-only connection check passed on 6000.6.7f1 (2026-10-26) |
| Build | `scripts/unity/Build-UnityPlayer.ps1` (Windows 64-bit Development); not yet run in this project. Distribution target undecided |
| Spec workflow | Lightweight: `SPEC_WORKFLOW.md`, `specs/NNN-name/` |
| Harness | `scripts/harness/Test-ProjectHarness.ps1` (required files, JSON, Unity version, .meta pairs, whitespace, secret patterns) |

## Adopted decisions
- Agent rules: `AGENTS.md` is canonical; `CLAUDE.md` imports it. Skills canonical in `.agents/skills/` with thin `.claude/skills/` entries.
- Unity automation ported from CookingShot (verified there on 6000.3): on-demand start, bounded waits, explicit Stop; no permanently registered MCP server. Shares port 8080 with CookingShot, so one project at a time.
- Blender tooling is intentionally not included.

## Maintenance
Keep confirmed facts/decisions here and continuation state in `HANDOFF.md`; replace obsolete state rather than append history. Update version facts when authoritative settings change.

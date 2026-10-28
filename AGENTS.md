# UnitySlotTower — agent rules

Applies below this directory; explicit user requests and narrower AGENTS.md take precedence. Reply in the user's language. Claude Code loads this file through `CLAUDE.md`; Codex/OpenCode read it directly.

## Read only what the task needs
- Answer known project-rule questions from these rules, citing paths; do not reread them. Batch known relevant reads. Stop investigating when evidence is sufficient; never invent unread contents.
- Broad project facts: `docs/ai/PROJECT_CONTEXT.md`. For a specific fact, read its authoritative source directly. When resuming ongoing work (including a switch of AI/model), read the current-state section of `docs/ai/HANDOFF.md`, then verify its pointers against the actual specs, source and test results.
- Confirm Unity in `ProjectSettings/ProjectVersion.txt`, packages in `Packages/manifest.json` / `packages-lock.json`; do not assume versions.
- Search relevant paths first (`Assets/UnitySlotTower/`, `docs/`, `scripts/`, `specs/`), widening only if needed. Exclude Library, Temp, Obj, Logs, UserSettings, .tools, Artifacts, Builds, .git and package caches unless diagnosing a specific item. Never dump all source/logs, generated csproj/sln or large assets.

## Change discipline
- Check Git status before changes; preserve existing user work. Establish expected behavior or acceptance criteria, then make the smallest consistent change. No unrelated formatting, renames, redesign, or package/Unity/render/input upgrades. Store no secrets or personal data.
- Significant features, spec changes or major fixes (or an explicit request): follow the `spec` skill (`.agents/skills/spec/SKILL.md`, procedure in `docs/ai/SPEC_WORKFLOW.md`). Important ambiguity is asked, not guessed; implementation needs the user's explicit approval of the current spec. Small edits need no documents.
- Keep confirmed facts in PROJECT_CONTEXT and continuation state in HANDOFF (replace stale state; never paste conversation history). Once a spec exists, link to it instead of duplicating requirements. Ask only questions whose answers change the work.

## Unity integrity
- Game code lives under `Assets/UnitySlotTower/` with asmdefs once gameplay code exists (Runtime / Editor / Tests separated). No UnityEditor in Runtime.
- Keep assets and .meta together; preserve GUIDs on moves/renames, never regenerate existing .meta. Edit scenes/prefabs/assets via the Editor (MCP); if text editing is unavoidable, limit changes and validate GUID/fileID/references.
- Inspect serialized data before field changes; use `FormerlySerializedAs` when renaming. Preserve Unity 6000.6 / URP / Input System patterns already in use (new Input System only; `InputSystem_Actions.inputactions`).
- Match destruction, event unsubscription and async cancellation (`destroyCancellationToken`, `Awaitable`) to lifetimes; Unity API on the main thread. Avoid hot-path `Find*`/`GetComponent`/LINQ/allocations/logs. Measure performance claims.

## Automation lifecycle
- Unity: `scripts/unity/Invoke-UnityMcp.ps1` starts/connects on demand with bounded waits; do not call Start separately. Keep connected during consecutive Unity operations and until async jobs reach final status.
- Before switching to work that needs no Unity connection, confirm results/saves, run `pwsh -NoProfile -File scripts/unity/Stop-UnityAutomation.ps1` and verify shutdown. Also stop before final reporting or abandoning failed Unity work. Never resend startup or mutations with unknown outcomes; never claim stopped without confirmation.
- CookingShot shares port 8080: only one project's automation at a time.

## Verification
- Code/assets/dependencies/agent configuration changes: run `pwsh -NoProfile -File scripts/harness/Test-ProjectHarness.ps1` (PowerShell 7). Prose-only edits: check content/links/diff.
- Unity code/assets: read the relevant sections of `docs/ai/UNITY_CHECKS.md`. Editor operations/tests/builds/images: read `docs/ai/UNITY_AUTOMATION.md`. Check instance/scene before operating; reconnect after Play/recompile. Verify test counts/failures, build job final status and actual images; distinguish Editor from player.
- Perform the minimum meaningful verification. Never count unrun checks as passing; report what was not verified. Update HANDOFF for significant continuation work.

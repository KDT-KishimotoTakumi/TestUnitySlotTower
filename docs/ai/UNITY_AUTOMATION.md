# Unity automation

Unity 6000.6.7f1; MCP for Unity 10.2.0 and matching package CLI (ported from the CookingShot setup verified on 6000.3).
Use MCP for Editor/scene/prefab/Inspector/Console/tests/builds; use file editing for code.

## Routine check / startup

Run from project root in PowerShell 7; use `pwsh -NoProfile -File` from other shells.

For **only instance/active-scene/Console inspection**, run `pwsh -NoProfile -File scripts/unity/Test-UnityConnection.ps1`. It starts on demand, waits for scene access, collects instance and up to 10 Console errors, and calls Stop in finally on success/failure. It verifies port 8080 closed and returns JSON. It never edits/saves scenes, enters Play, runs tests or builds. For continuing Unity operations, use Invoke below and keep the connection.

```powershell
# Initial setup / missing .tools only; Python 3.12.
./scripts/unity/Setup-UnityAutomation.ps1
# Automatically starts server and, if needed, Editor.
./scripts/unity/Invoke-UnityMcp.ps1 instance list
./scripts/unity/Invoke-UnityMcp.ps1 scene active
# After results and necessary saves are confirmed; leaves Editor open.
./scripts/unity/Stop-UnityAutomation.ps1
```

## Bounds, ownership and failure
- Local endpoint: `127.0.0.1:8080/mcp`; no cloud account. Normally do not call Start separately.
- Runner stages: startup (50s), read-only scene readiness (default 120s), requested operation (default 60s). Progress/errors go to stderr; operation JSON to stdout. Only readiness probes retry; startup and mutations do not.
- Child input/output are isolated from caller pipes. CLI hangs are bounded; a startup timeout never force-closes Editor.
- Allow at least 270s caller timeout for an individual Invoke, including process cleanup. Configure positive finite seconds via `SLOTTOWER_MCP_READY_TIMEOUT` / `SLOTTOWER_MCP_COMMAND_TIMEOUT` environment variables and adjust the caller too. The routine-check script contains several Invokes; allow up to 720s for the complete check.
- If initial import exceeds readiness time, inspect `Logs/Automation/editor.log` and confirm the requested operation was not sent before trying again. Instance/status and build/test polling bypass scene readiness; help does not start automation.
- Start's `MCP server healthy ...` means HTTP health only. Probe with `Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 3`; use CLI `instance list` for Editor connections.
- Timeout does not prove a mutation was unexecuted. Inspect state/jobs; never resend unknown-outcome creation/deletion/build commands. Parallel startup requests are rejected; do not run CLI operations concurrently with Stop.
- Normal Editor launch does not start MCP. Start creates `UserSettings/SlotTowerAutomation.enabled`; Editor connects after compilation. An already-open Editor attempts connection within a few seconds.
- Stop removes the enable marker, requests Editor disconnection, then stops only its recorded/validated server PID. If an open Editor does not acknowledge within 30s, leave the server running and retry Stop after compilation. Never close external servers or Editor. Manual connection fallback: `Tools > UnitySlotTower > Automation > Connect`.
- MCP endpoint/uvx path also affect EditorPrefs; check them when switching projects with different connections.
- CookingShot uses the same port 8080. Run only one project's automation at a time: Stop the other project first and confirm 8080 is closed.

## AI lifecycle and commands
Keep connected through consecutive Unity operations and until async jobs reach final status. Before code/docs/research needing no connection, confirm results/necessary saves, Stop and verify. Also stop before final reporting or abandoning failed Unity work. Report shutdown failures honestly. There is no idle timer; after forcibly terminating the AI, manually run Stop.

Select the target instance before operating. Wrapper selects UnitySlotTower; disambiguate same-name projects with an instance ID.

```powershell
./scripts/unity/Invoke-UnityMcp.ps1 scene hierarchy --limit 20 --max-depth 2
./scripts/unity/Invoke-UnityMcp.ps1 editor console --type error --count 10
./scripts/unity/Invoke-UnityMcp.ps1 editor play
# Reconfirm after Play/reload.
./scripts/unity/Invoke-UnityMcp.ps1 scene active
./scripts/unity/Invoke-UnityMcp.ps1 camera screenshot --file-name editor-play.png --output-folder Artifacts/Automation/Editor
# Confirm generated image before stopping Play.
./scripts/unity/Invoke-UnityMcp.ps1 editor stop
```

- Scene View capture: add `--capture-source scene_view`. Images may be saved asynchronously: verify PNG existence and open it; a success message alone is insufficient.
- Operations return `status` + `result`; inspect `result.success` and final job state. Version 10.2.0 diagnostics such as instance lists can return text despite JSON selection; wrapper preserves that text.

## Tests
```powershell
./scripts/unity/Invoke-UnityMcp.ps1 editor tests --mode EditMode --async
./scripts/unity/Invoke-UnityMcp.ps1 editor poll-test <job_id> --wait 5
# Select PlayMode when applicable.
```
Check `summary.total`, `passed`, `failed`. Zero tests verifies the execution path, not feature behavior.

## Player build / images
Stop Play and confirm reconnection first.

```powershell
./scripts/unity/Build-UnityPlayer.ps1
./scripts/unity/Test-UnityPlayer.ps1 -ShowWindow
```

- Builds enabled scenes into Windows 64-bit Development `Builds/Windows/UnitySlotTower.exe`, not a distribution-platform decision. Uses MCP `manage_build`; inspect jobs after timeout, never force-close Editor.
- Player starts at 1280×720, captures after about 3s, then exits. ShowWindow displays the player during capture (approved during initial verification); hidden capture failed on this PC. Capture requires Development Build and `-automationScreenshot`; normal/release launches are unaffected.
- Each run uses a new output folder; never reuse old images as evidence. Wrapper checks exit code/PNG header and writes `result.json`. Runtime errors/exceptions fail verification.
- Open the PNG and compare expected appearance. This is startup verification, not gameplay/input correctness; add appropriate scenarios/tests for those. Never use a `-nographics` player for image verification.

## Output / diagnosis
| Path | Purpose |
| --- | --- |
| `.tools/unity-mcp/` | Pinned Python server/CLI; reproducible, do not edit |
| `Logs/Automation/` | Server/Editor logs |
| `Artifacts/Automation/Editor/` | Editor captures |
| `Artifacts/Automation/Build/` | Build requests/results |
| `Artifacts/Automation/Player/` | Images, player logs/results |
| `Builds/Windows/` | Player build |

Exclude these from Git/routine searches; inspect only relevant diagnostic tails/error context. Diagnose process → HTTP health → Editor compile errors → instance list. Wait for reconnection after Play-related 503; do not duplicate unknown-outcome operations or launch a second Editor for the same project.

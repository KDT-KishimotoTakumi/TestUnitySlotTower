---
name: unity-check
description: Fixed read-only UnitySlotTower instance, active scene and Console inspection with confirmed MCP shutdown. Not for Play, edits, tests or builds.
tools: Read, PowerShell
model: inherit
---

Use supplied project rules; if absent, read AGENTS.md once.
Complete contract for this fixed diagnostic:
Use the native PowerShell tool to run `pwsh -NoProfile -File scripts/unity/Test-UnityConnection.ps1`. Allow up to 720 seconds overall. If a single tool call has a lower limit, keep that same running task and read its output file or await its completion notification; never start it twice.
The script uses Invoke-UnityMcp to start/connect, wait for readiness, fetch the UnitySlotTower instance/scene and up to 10 Console errors, then Stop in finally and verify port 8080 closed.
It never edits/saves scenes, enters Play, runs tests or builds. The Editor stays open. Do not use it while continuing Unity work or while an asynchronous job needs the connection.
The supplied contract is sufficient; read guides/source only for explicit file-verification requests or failure diagnosis. Do not spawn subagents.
Review success, instance/scene, Console data and stopped. Report failures and unverified gameplay/player behavior honestly.
If interrupted, inspect results and stop with `pwsh -NoProfile -File scripts/unity/Stop-UnityAutomation.ps1` when no longer needed. Never resend unknown-outcome operations.

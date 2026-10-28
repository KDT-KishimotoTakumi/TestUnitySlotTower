@AGENTS.md

## Claude Code specifics
- Use the native PowerShell tool for project scripts (`pwsh -NoProfile -File ...`). For long Unity scripts, run in the background and wait for the completion notification; never start the same mutation twice.
- Project skills: `unity-verify` (change-specific verification), `unity-editor` (Editor/MCP operations), `spec` (significant features), `handoff` (checkpoint for resuming). Load a skill only when its task applies.
- Agents: `unity-check` (read-only instance/scene/Console check with shutdown), `code-task` (small edits at known paths). Use a normal session for broader work. Do not spawn subagents for routine tasks.
- Context hygiene: the user clears the session regularly. At each natural break (a milestone or task group in `tasks.md` done, a stage transition, or before switching to unrelated work), stop Unity automation if running, update `docs/ai/HANDOFF.md` with the `handoff` skill, commit if the user's workflow calls for it, and then tell the user it is a good point to run `/clear` and resume with 「HANDOFF を読んで続きから」. After `/clear`, resume by reading only the HANDOFF current-state section and the files it points to.
- Unity MCP is not registered as a permanent MCP server on purpose (keeps tool schemas out of context and the Editor disconnected when idle); the CLI wrapper covers Editor operations.

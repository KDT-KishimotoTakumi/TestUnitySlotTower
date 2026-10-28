"""On-demand Unity MCP lifecycle. Stdout is reserved for the CLI response."""

import contextlib
import json
import math
import os
from pathlib import Path
import shutil
import sys
import time


ROOT = Path(__file__).resolve().parents[2]
CLI = ROOT / ".tools/unity-mcp/Scripts/unity-mcp.exe"
sys.path.insert(0, str(ROOT / "scripts/automation"))
from bounded_process import AutomationError, run_bounded


def progress(message):
    print(f"[Unity MCP] {message}", file=sys.stderr, flush=True)


@contextlib.contextmanager
def startup_lock():
    import msvcrt

    directory = ROOT / "Logs/Automation"
    directory.mkdir(parents=True, exist_ok=True)
    with (directory / "start.lock").open("a+b") as handle:
        handle.seek(0)
        if not handle.read(1):
            handle.write(b"0")
            handle.flush()
        handle.seek(0)
        try:
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
        except OSError as error:
            raise AutomationError("Another Unity startup is in progress; do not start a second one.") from error
        try:
            yield
        finally:
            handle.seek(0)
            msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)


def ensure_started():
    with startup_lock():
        shell = shutil.which("pwsh.exe") or shutil.which("powershell.exe")
        if not shell:
            raise AutomationError("PowerShell was not found.")
        progress("Ensuring server and Editor are started (limit 50s)...")
        code, output, error = run_bounded(
            [shell, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
             ROOT / "scripts/unity/Start-UnityAutomation.ps1"],
            50, "Startup", kill_tree=False,
        )
        if code:
            raise AutomationError(f"Startup failed ({code}): {error.strip()} {output.strip()}")


def cli_command(arguments, timeout):
    # Unity registers the instance under the project folder name.
    return [CLI, "--host", "127.0.0.1", "--port", "8080", "--instance", ROOT.name,
            "--format", "json", "--timeout", str(max(1, math.ceil(timeout))), *arguments]


def validate_response(code, output, error, *, allow_text=False):
    if code:
        raise AutomationError(f"CLI failed ({code}): {error.strip()} {output.strip()}")
    try:
        envelope = json.loads(output)
    except ValueError as exception:
        # MCP 10.2.0's instance/status diagnostics ignore --format json.
        if allow_text and output.strip():
            return None
        raise AutomationError(f"CLI returned invalid JSON: {output[:1000]} {error[:1000]}") from exception
    if not isinstance(envelope, dict) or envelope.get("status") != "success":
        raise AutomationError(f"Unity rejected the operation: {output.strip()}")
    result = envelope.get("result")
    if isinstance(result, dict) and result.get("success") is False:
        raise AutomationError(f"Unity rejected the operation: {output.strip()}")
    return envelope


def wait_ready(timeout):
    progress(f"Waiting for UnitySlotTower scene access (limit {timeout:g}s)...")
    deadline = time.monotonic() + timeout
    last_error = "No ready response."
    while time.monotonic() < deadline:
        remaining = deadline - time.monotonic()
        try:
            probe_timeout = min(10, remaining)
            result = run_bounded(cli_command(["scene", "active"], probe_timeout),
                                 probe_timeout, "Read-only readiness probe")
            validate_response(*result)
            return
        except AutomationError as error:
            last_error = str(error)
        remaining = deadline - time.monotonic()
        if remaining > 0:
            time.sleep(min(2, remaining))
    raise AutomationError(
        f"Editor readiness timed out after {timeout:g}s; requested operation was NOT sent. "
        f"Check Logs/Automation/editor.log for import/compile progress. Last probe: {last_error}"
    )


def is_diagnostic(arguments):
    # These commands must remain usable while the Editor is compiling/building.
    if arguments[:1] in (["instance"], ["instances"], ["status"]):
        return True
    if arguments[:2] in (["editor", "poll-test"], ["build", "status"]):
        return True
    if arguments[:2] == ["raw", "manage_build"] and len(arguments) == 3:
        try:
            return json.loads(arguments[2]).get("action") == "status"
        except (ValueError, AttributeError):
            pass
    return False


def positive_seconds(name, default):
    value = float(os.environ.get(name, default))
    if not math.isfinite(value) or value <= 0:
        raise AutomationError(f"{name} must be a finite positive number.")
    return value


def execute(arguments):
    if not CLI.is_file():
        raise AutomationError("Run Setup-UnityAutomation.ps1 first.")
    ready_timeout = positive_seconds("SLOTTOWER_MCP_READY_TIMEOUT", 120)
    command_timeout = positive_seconds("SLOTTOWER_MCP_COMMAND_TIMEOUT", 60)
    help_only = not arguments or "--help" in arguments or arguments == ["--version"]
    if not help_only:
        ensure_started()
        if not is_diagnostic(arguments):
            wait_ready(ready_timeout)
    # Never retry this call: mutations and asynchronous job creation are not idempotent.
    code, output, error = run_bounded(cli_command(arguments, command_timeout),
                                      command_timeout, "CLI operation")
    if error:
        print(error, file=sys.stderr, end="", flush=True)
    if output:
        print(output, end="", flush=True)
    if help_only:
        if code:
            raise AutomationError(f"CLI failed ({code}).")
    else:
        validate_response(code, output, error,
                          allow_text=arguments[:1] in (["instance"], ["instances"], ["status"]))


def main():
    os.environ["PYTHONIOENCODING"] = "utf-8"
    os.environ["PYTHONUTF8"] = "1"
    try:
        execute(sys.argv[1:])
        return 0
    except (AutomationError, OSError, ValueError) as error:
        progress(str(error))
        return 1
    except KeyboardInterrupt:
        progress("Interrupted. Inspect operation/job status before retrying; stop when no longer needed.")
        return 130


if __name__ == "__main__":
    sys.exit(main())

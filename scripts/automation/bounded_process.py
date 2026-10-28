"""Bound native automation calls without waiting for inherited output-pipe EOF."""

from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]


class AutomationError(RuntimeError):
    pass


def run_bounded(command, timeout, phase, *, kill_tree=True):
    with tempfile.TemporaryFile() as stdout, tempfile.TemporaryFile() as stderr:
        process = subprocess.Popen(
            [str(arg) for arg in command], cwd=ROOT, stdin=subprocess.DEVNULL,
            stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW,
        )
        try:
            process.wait(timeout=timeout)
        except (subprocess.TimeoutExpired, KeyboardInterrupt) as error:
            if kill_tree:
                try:
                    subprocess.run(
                        ["taskkill.exe", "/PID", str(process.pid), "/T", "/F"],
                        stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL, timeout=5,
                        creationflags=subprocess.CREATE_NO_WINDOW,
                    )
                except (OSError, subprocess.TimeoutExpired):
                    pass
            # Startup children include the Editor/worker; never kill that tree.
            if process.poll() is None:
                process.kill()
            process.wait(timeout=5)
            if isinstance(error, KeyboardInterrupt):
                raise
            raise AutomationError(
                f"{phase} timed out after {timeout:g}s. Operation outcome may be unknown; "
                "do not resend mutations. Inspect the automation logs and job status. "
                "Save/confirm results and run the corresponding Stop script when done."
            ) from error
        stdout.seek(0)
        stderr.seek(0)
        return (process.returncode, stdout.read().decode("utf-8-sig", errors="replace"),
                stderr.read().decode("utf-8-sig", errors="replace"))

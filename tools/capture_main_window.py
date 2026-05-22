from __future__ import annotations

import subprocess
import sys
import time
from pathlib import Path

from pywinauto import Desktop


REPO_ROOT = Path(r"C:\Users\Gyrocopter_UA\Desktop\FluxorProject\Fluxor")
APP_EXE = REPO_ROOT / "CursorFX.App" / "bin" / "Debug" / "net9.0-windows" / "Fluxor.exe"
OUTPUT = REPO_ROOT / "docs" / "screenshots" / "main-window.png"


def wait_for_window(timeout: float = 20.0):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            window = Desktop(backend="uia").window(title="Fluxor")
            if window.exists(timeout=0.5):
                return window
        except Exception:
            pass
        time.sleep(0.5)
    return None


def main() -> int:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)

    for attempt in range(1, 4):
        proc = subprocess.Popen([str(APP_EXE)])
        window = wait_for_window()
        if window is None:
            if proc.poll() is None:
                proc.terminate()
            continue

        stable_until = time.time() + 18.0
        while time.time() < stable_until:
            if proc.poll() is not None:
                break
            time.sleep(0.5)

        if proc.poll() is not None:
            continue

        window.set_focus()
        time.sleep(1.0)
        window.capture_as_image().save(OUTPUT)

        try:
            proc.terminate()
            proc.wait(timeout=5)
        except Exception:
            try:
                proc.kill()
            except Exception:
                pass

        print(str(OUTPUT))
        return 0

    print("Failed to keep Fluxor alive long enough to capture the main window.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

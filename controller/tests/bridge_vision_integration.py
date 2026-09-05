import json
import subprocess
import sys
import time

PREFIX = "@@RESPONSE@@"


def send(process, command):
    process.stdin.write(command + "\n")
    process.stdin.flush()
    while True:
        line = process.stdout.readline()
        if not line:
            raise RuntimeError("bridge exited before responding")
        if line.startswith(PREFIX):
            return json.loads(line[len(PREFIX):])


def run_cycle(process, sample):
    assert send(process, "cycle-sample-" + sample)["success"] is True
    visited = set()
    deadline = time.time() + 8
    status = None
    while time.time() < deadline:
        status = send(process, "status")
        visited.add(status["cycle"]["state"])
        if status["cycle"]["state"] in ("CycleComplete", "CycleFaulted"):
            return status, visited
        time.sleep(0.02)
    raise AssertionError("cycle did not finish")


def main():
    process = subprocess.Popen(
        [sys.argv[1]], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, text=True, bufsize=1
    )
    try:
        assert send(process, "initialize")["success"] is True
        assert send(process, "start")["success"] is True

        passed, states = run_cycle(process, "good-part")
        assert "MovingToAcceptBin" in states
        assert passed["inspection"]["accepted"] is True
        assert passed["inspection"]["reason"] == "PASS"

        rejected, states = run_cycle(process, "missing-hole")
        assert "MovingToRejectBin" in states
        assert rejected["inspection"]["accepted"] is False
        assert rejected["inspection"]["reason"] == "MISSING_FEATURE"

        malformed, states = run_cycle(process, "malformed-part")
        assert "MovingToRejectBin" in states
        assert malformed["inspection"]["reason"] == "GEOMETRY_MISMATCH"

        failed, _ = run_cycle(process, "unreadable-part")
        assert failed["state"] == "Faulted"
        assert failed["fault"]["code"] == "INSPECTION_FAILURE"
        assert failed["inspection"]["reason"] == "INSPECTION_ERROR"
    finally:
        if process.poll() is None:
            try:
                send(process, "exit")
            except Exception:
                process.kill()
        process.wait(timeout=5)


if __name__ == "__main__":
    main()

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
            raise RuntimeError("machine_bridge closed its output")
        if line.startswith(PREFIX):
            return json.loads(line[len(PREFIX):])


def main():
    process = subprocess.Popen(
        [sys.argv[1]],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
    )

    try:
        assert send(process, "cycle-accepted")["success"] is False
        assert send(process, "initialize")["success"] is True
        assert send(process, "cycle-accepted")["success"] is False
        assert send(process, "start")["success"] is True

        started = send(process, "cycle-accepted")
        assert started["success"] is True
        assert started["partSensor"]["active"] is True
        assert send(process, "cycle-rejected")["success"] is False

        # No commands are sent while the controller-owning thread advances
        # the complete simulated production sequence.
        time.sleep(2.5)

        completed = send(process, "status")
        assert completed["cycle"] == {
            "state": "CycleComplete",
            "total": 1,
            "accepted": 1,
            "rejected": 0,
        }
        assert completed["robot"] == {
            "position": "Home",
            "moving": False,
            "initialized": True,
        }
        assert completed["conveyor"]["running"] is True
        assert completed["gripper"]["open"] is True
        assert completed["partSensor"]["active"] is False

        assert send(process, "cycle-rejected")["success"] is True
        time.sleep(2.5)
        rejected = send(process, "status")
        assert rejected["cycle"]["state"] == "CycleComplete"
        assert rejected["cycle"]["total"] == 2
        assert rejected["cycle"]["accepted"] == 1
        assert rejected["cycle"]["rejected"] == 1

        assert send(process, "cycle-accepted")["success"] is True
        time.sleep(0.05)
        stopped = send(process, "estop")
        assert stopped["success"] is True
        assert stopped["state"] == "EmergencyStop"
        assert stopped["emergencyStopActive"] is True
        assert stopped["robot"]["moving"] is False
        assert stopped["conveyor"]["running"] is False
    finally:
        if process.poll() is None:
            process.stdin.write("exit\n")
            process.stdin.flush()
            try:
                process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=3)
                raise

    assert process.returncode == 0


if __name__ == "__main__":
    main()

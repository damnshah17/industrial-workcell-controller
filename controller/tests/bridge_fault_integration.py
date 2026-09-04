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


def wait_for_fault(process, expected_code, timeout=5):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        status = send(process, "status")
        if status["state"] == "Faulted":
            assert status["fault"]["code"] == expected_code
            return status
        time.sleep(0.03)
    raise AssertionError(f"machine did not fault with {expected_code}")


def run_scenario(executable, simulation_fault, expected_code):
    process = subprocess.Popen(
        [executable],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        bufsize=1,
    )
    try:
        assert send(process, "initialize")["success"] is True
        assert send(process, "start")["success"] is True
        assert send(process, f"simulation-fault-{simulation_fault}")["success"] is True
        send(process, "cycle-accepted")
        wait_for_fault(process, expected_code)

        assert send(process, f"simulation-fault-{simulation_fault}-clear")["success"] is True
        reset = send(process, "reset")
        assert reset["success"] is True
        assert reset["state"] == "Idle"
        assert reset["fault"] is None
        assert send(process, "start")["success"] is True
        assert send(process, "cycle-accepted")["success"] is True
    finally:
        if process.poll() is None:
            process.stdin.write("exit\n")
            process.stdin.flush()
            process.wait(timeout=3)


def main():
    executable = sys.argv[1]
    scenarios = (
        ("robot-communication", "ROBOT_COMMUNICATION_LOSS"),
        ("motion-timeout", "MOTION_TIMEOUT"),
        ("conveyor-stop", "CONVEYOR_FAILURE"),
        ("conveyor-start", "CONVEYOR_FAILURE"),
        ("gripper-close", "GRIPPER_FAILURE"),
        ("gripper-open", "GRIPPER_FAILURE"),
        ("sensor", "SENSOR_FAILURE"),
        ("safety-door", "SAFETY_DOOR_OPEN"),
    )
    for simulation_fault, expected_code in scenarios:
        run_scenario(executable, simulation_fault, expected_code)


if __name__ == "__main__":
    main()

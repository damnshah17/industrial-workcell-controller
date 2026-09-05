import json
import socket
import subprocess
import sys
import time


def request(stream, request_id, command, payload=None):
    message = {"requestId": request_id, "command": command, "payload": payload or {}}
    stream.write((json.dumps(message, separators=(",", ":")) + "\n").encode())
    stream.flush()
    return json.loads(stream.readline())


def main():
    probe = socket.socket()
    probe.bind(("127.0.0.1", 0))
    port = probe.getsockname()[1]
    probe.close()
    process = subprocess.Popen(
        [sys.argv[1], "--tcp-port", str(port)],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    client = socket.socket()
    deadline = time.monotonic() + 5
    while True:
        try:
            client.connect(("127.0.0.1", port))
            break
        except ConnectionRefusedError:
            if process.poll() is not None or time.monotonic() >= deadline:
                raise
            time.sleep(0.025)

    stream = client.makefile("rwb")
    try:
        stream.write(b"not-json\n")
        stream.flush()
        malformed = json.loads(stream.readline())
        assert malformed["success"] is False
        assert malformed["error"]["code"] == "MALFORMED_REQUEST"

        status = request(stream, "status-1", "status")
        assert status["requestId"] == "status-1"
        assert status["success"] is True
        assert status["status"]["state"] == "Offline"

        unknown = request(stream, "unknown-1", "not-a-command")
        assert unknown["requestId"] == "unknown-1"
        assert unknown["success"] is False
        assert unknown["error"]["code"] == "UNKNOWN_COMMAND"

        assert request(stream, "init", "initialize")["success"] is True
        assert request(stream, "start", "start")["status"]["state"] == "Running"
        vision = request(stream, "vision", "start-cycle", {"sampleId": "good-part"})
        assert vision["requestId"] == "vision"
        assert vision["success"] is True

        fault = request(
            stream, "fault", "configure-simulation-fault",
            {"fault": "robot-communication", "enabled": True}
        )
        assert fault["requestId"] == "fault"
        assert fault["success"] is True, fault

        for index in range(25):
            correlated = request(stream, f"serial-{index}", "status")
            assert correlated["requestId"] == f"serial-{index}"

        shutdown = request(stream, "shutdown-1", "shutdown")
        assert shutdown["requestId"] == "shutdown-1"
        assert shutdown["success"] is True
    finally:
        stream.close()
        client.close()
    process.wait(timeout=3)
    assert process.returncode == 0


if __name__ == "__main__":
    main()

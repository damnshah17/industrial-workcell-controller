# Advanced failure simulation

The simulation API configures failure behavior in the C++ simulated devices. It does not directly set the machine state. During a production cycle, the real `SequenceController` observes the resulting device behavior and raises the appropriate controller fault.

## Endpoints

Enable a failure with `POST /api/simulation/faults/{fault}` and clear it with `POST /api/simulation/faults/{fault}/clear`.

Supported fault names:

- `robot-communication`
- `motion-timeout`
- `conveyor-start`
- `conveyor-stop`
- `gripper-open`
- `gripper-close`
- `sensor`
- `safety-door`

Clear every configured simulation condition with:

```http
POST /api/simulation/faults/clear
```

After a controller fault, clear the simulated condition first and then call the normal machine reset endpoint:

```http
POST /api/simulation/faults/gripper-close/clear
POST /api/machine/reset
POST /api/machine/start
```

Simulation endpoints are intentionally separate from `/api/machine`. The existing direct motion-timeout injection endpoint remains available for backward compatibility, while `/api/simulation/faults/motion-timeout` exercises the natural robot-stall and sequence-timeout path.

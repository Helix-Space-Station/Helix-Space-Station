droppod-dispatch-console-title = Drop Pod Dispatch
droppod-dispatch-console-cargo-header = CARGO / PASSENGERS
droppod-dispatch-console-ghost-header = SPAWN ON LANDING
droppod-dispatch-console-beacon-header = LANDING BEACON
droppod-dispatch-console-launch = LAUNCH DROP POD
droppod-dispatch-console-no-beacon = No beacon selected.
droppod-dispatch-console-empty = Nothing in range.
droppod-dispatch-console-cooldown = Status: Cooldown { $seconds }s
droppod-dispatch-console-status-ready = Status: Ready to launch.
droppod-dispatch-console-status-not-ready = Status: Not ready.
droppod-dispatch-console-status-unpowered = Status: Unpowered.

droppod-dispatch-popup-launched = Drop pod launched.
droppod-dispatch-popup-need-target = Select a landing beacon.
droppod-dispatch-popup-need-cargo = Load cargo or choose a landing spawn.
droppod-dispatch-popup-cooldown = Console is recharging ({ $seconds }s).
droppod-dispatch-popup-unpowered = Console is unpowered.
droppod-dispatch-popup-insert-fail = Failed to launch the drop pod.

ent-ComputerDroppodDispatch = drop pod dispatch console
    .desc = Loads nearby personnel and cargo into a drop pod and launches it at a station beacon.
    .suffix = Droppod

ent-DroppodLoadPad = drop pod load pad
    .desc = Stand here with cargo. The dispatch console will load whatever is on this pad into the next pod.
    .suffix = Droppod

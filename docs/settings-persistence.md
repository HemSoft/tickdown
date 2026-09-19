# Settings persistence

TickDown uses one ordered settings queue per application instance. A save freezes
its JSON before returning a task. Later model edits cannot change that snapshot.
Reads and recovery use the same queue, so a recovery cannot move a newer save.
Use one application instance per settings directory.

Each write creates a unique temporary file beside the destination, flushes its
contents to disk, then replaces the destination. Existing valid settings remain
in a `.bak` file. Incomplete `.tmp` files are never loaded. Tests simulate an
interruption before replacement; they do not simulate power loss or filesystem
hardware failure.

A missing file is a normal first run. Malformed JSON and access failures are not
empty settings. When a valid backup exists, TickDown loads it and shows a message.
When possible, recovery copies malformed data to a `.corrupt.<sha256>` file.
The content fingerprint prevents repeated reads of one undeletable primary from
creating duplicate archives. If archiving or removal is blocked, the corrupt
primary stays in place and the validated backup remains usable. Without a valid backup, the original remains
in place and subsequent loads still report the failure. Do not remove these
files until recovery is complete.

The settings banner records the latest failure or backup recovery for the current
session. A save failure leaves the prior committed data available. After fixing
the access problem, edit the affected setting again to retry its current snapshot.
The banner remains as a record until restart.

Closing disables input, saves geometry, and waits for the queue to drain. An
unresolved write failure cancels closing instead of silently discarding work.
A successful retry of the affected file clears its unresolved write failure.
Theme changes enqueue their snapshots before yielding, so the shutdown flush
includes them. Preserving the selected theme in the geometry snapshot is separate
tracked work.

## Validation

`dotnet test TickDown.sln -c Release` exercises the actual settings-service source
against temporary directories. Cases include 32 overlapping saves of 1,000 timers,
frozen snapshots, final-write ordering, missing and empty files, malformed data,
backup recovery, a locked destination, retry, shutdown flush, interrupted temporary
files, and recovery concurrent with a new save.

For native validation, launch the built app with `TICKDOWN_SETTINGS_DIRECTORY`
set to a disposable directory. Never use real user settings for corruption or
file-lock tests. Verify the visible banner, recovery from a seeded backup, and
that closing waits for writes or remains canceled after a failed save.

# Windows desktop qualification

`scripts/desktop-qualification.ps1` drives the built WinUI application through Windows UI Automation. It launches the candidate with a disposable `TICKDOWN_SETTINGS_DIRECTORY`; the harness never reads or writes the normal TickDown profile.

## Commands

Fast local feedback:

```powershell
pwsh -NoProfile -File scripts/desktop-qualification.ps1 -Mode Fast -RunLabel local
```

Full qualification with screenshots and an MP4 recording:

```powershell
pwsh -NoProfile -File scripts/desktop-qualification.ps1 -Mode Full -RunLabel full -CaptureEvidence
```

The candidate must be a clean checkout. In CI, `GITHUB_SHA` must equal `HEAD`. Outputs are restricted to `artifacts/desktop-qualification/<commit>/<run>/` and include candidate-bound JSON and Markdown reports. `-AllowDirty` exists only for harness development; such a report records `WorkingTreeClean` as false and is not release evidence.

## Journey and measurements

The workload recovers a valid backup behind a corrupt primary, then covers add, edit, start, pause, resume, completion, dismiss, stop, remove, restart, alarm-repeat cleanup, theme switching, keyboard focus/tab order, keyboard zoom, accessible control names, and the declared high-contrast resource. UI activity stays on the configured qualification display.

After warm-up cycles, the harness records:

- UI input latency and queued tick-to-display p50, p95, p99, and maximum latency;
- displayed-tick throughput and maximum pending UI callbacks;
- managed heap separately from process private memory and working set;
- handle and thread deltas;
- final timer, tick-subscription, alarm-repeat timer, media-player, and pending-callback counts.

`desktop-qualification-policy.json` defines timer counts, sample durations, and budgets. Memory budgets scale by the square root of runtime-available memory relative to the reference machine; queue limits scale with the timer count. Final ownership counters must settle to zero. Growth over a budget is a regression signal, not by itself a leak claim; investigate retained managed objects and native/process ownership before classifying a leak.

`Fast` is bounded feedback. `Full` is the final qualification tier and is intended for delivery CI and comparable repeated samples. Keep generated evidence out of source control and publish the current-head report, screenshots, and recording with the pull request.

# Countdown mutation gate

TickDown pins Stryker.NET 5.0.0 in `dotnet-tools.json`. That release targets
.NET 10 directly and does not roll forward to another tool version. The initial
scope is the hand-written production countdown state machine only:
`src/TickDown.Core/Models/CountdownTimer.cs`. No generated source exclusion is
needed because the positive mutate glob selects that one file.

Run the gate from a clean candidate revision after locked restore:

```powershell
pwsh -NoProfile -File scripts/check-mutation.ps1
```

The wrapper restores the pinned local tool, verifies that the checkout is clean
and matches `GITHUB_SHA` when present, runs Stryker, and leaves these artifacts
under `artifacts/mutation`:

- the full JSON report with killed and surviving mutants;
- an interactive HTML report;
- `mutation-summary.md` with score, execution time, counts, and every survivor;
- `candidate.json` with commit, tool version, elapsed time, report SHA-256, and
  status counts; and
- the complete console log.

These fields bind every published result to the exact candidate revision without
sending source or a token to an external mutation dashboard.

## Measured baseline and thresholds

The first run created 65 mutants. Stryker skipped 21 before execution: 2 did not
compile, 9 were removed by its covered-block optimization, and 10 were outside
the positive mutate filter. Of 44 executed mutants, 33 were killed and 11
survived, for a measured score of 75.00% in 49.43 seconds.

Focused tests were then added for constructor/default behavior, intermediate
progress arithmetic, exact start boundaries, and stopped versus running, paused,
and completed duration updates. The maintained run killed all 44 executed
mutants, with 0 survivors, 0 timeouts, and 0 errors: 100.00% in 49.03 seconds.
This includes mutations that remove or invert the completion boundary and the
stopped/non-stopped duration guard.

The target and break thresholds are both 100. This is deliberate for the small,
bounded state machine: its real baseline has no survivor, so any future survivor
means the behavior tests no longer distinguish a logic change. Lowering either
threshold requires a reviewed rationale for each surviving or newly untested
mutation; it must not be used merely to make CI green.

`MutationQuality.Tests.ps1` validates the pinned tool/configuration and report
summarization. CI must run the full mutation command as its own job, upload the
entire artifact directory even on failure, and include that job in final merge
qualification.

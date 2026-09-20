# Windows merge qualification

The `Windows quality` workflow runs for every pull request and every push to
`main`. It has no path filter, so documentation-only changes cannot omit the
required result. Workflow permissions grant read-only repository contents.
Every third-party action uses a full commit SHA.

## Jobs

`Quality policy` performs locked restore, installs the locked npm tools, tests
the quality, dependency, style, coverage, mutation, desktop qualification and
required-job policies, then runs the complete repository quality policy. The
desktop policy assertions inspect the qualification runner and thresholds; the
workflow does not launch or automate the desktop application. That policy checks style,
whitespace, analyzers, direct and transitive NuGet vulnerabilities, outdated
NuGet packages, Markdown and npm vulnerabilities.

`Release build and tests` restores in locked mode, builds and tests the full
solution in Release configuration. `Coverage and function risk` publishes the
Cobertura report and complete/worst-function CRAP reports. `Countdown mutation`
runs the pinned 100% countdown state-machine gate and publishes its JSON, HTML,
summary, candidate binding and console log even when the threshold fails.
`Build (x64)`, `Build (x86)` and `Build (ARM64)` compile the WinUI
application with fail-fast disabled. The x64 Windows runner executes the tests.
The x86 and ARM64 candidates are compile-qualified because GitHub-hosted Windows
runners cannot execute those native combinations. TickDown targets
`net10.0-windows10.0.19041.0` with minimum Windows version `10.0.17763.0`.
No other operating system or processor architecture is claimed as supported.

`Required` runs with `always()` after all child jobs. It fails unless quality,
tests, coverage, mutation and the complete architecture matrix all report
`success`. Failure,
cancellation, an unexpected skip, malformed results or a missing child cannot
produce a successful final result. Workflow cancellation can cancel the final
job itself; a missing or cancelled required status still blocks the branch rule.

The 30-minute child timeouts and five-minute final timeout are safety ceilings,
not a feedback budget. Each child writes measured execution time to its job
summary. Coverage and mutation artifacts are retained for 14 days and remain
bound to the candidate SHA recorded by their reports. After the first representative pull request and `main` run, record API
job durations here, distinguish first failing feedback from final qualification,
and only then choose a feedback budget.

## Main rule policy

After the workflow is merged and has produced a successful `main` run, fetch its
check runs and use the exact final check name in one active ruleset for the
default branch. The intended policy is:

- require a pull request and resolution of every review conversation;
- require the current branch head and the exact final Windows status;
- reject failed, cancelled, missing and unexpectedly skipped final statuses;
- allow squash merges only;
- block force pushes and branch deletion;
- configure no standing bypass actors; and
- require zero formal approving reviews for this solo-maintainer repository.

Connected Codex review remains part of the repository's operating process, but
its comment review is not misrepresented as a GitHub approving-review status.
Signed commits and a release job are not required because GitHub creates the
squash merge and this repository does not publish an automated release. Changing
those owner policies requires a separate reviewed repository change.

There is no routine bypass. For an incident where the rule itself prevents a
security fix, the repository owner may temporarily edit the ruleset only after
opening an issue that records the reason, candidate commit and restoration step.
Restore the rule immediately after the fix and attach the audit-log evidence.

## Activation proof

Do not activate a guessed check name. Use this order:

1. Merge the workflow only after the complete local quality policy passes.
2. Fetch the successful `main` check runs and record the exact final name and app.
3. Create the active default-branch ruleset with that exact status.
4. Open a disposable proof pull request. Push one deliberate failing test and
   verify the failed required status blocks merge.
5. Revert the failing test on the same branch. Verify every child and the final
   status pass, then record pull-request and `main` job durations.
6. Merge the useful documentation update from that proof pull request, verify the
   `main` run, and fetch the live ruleset to detect configuration drift.

A skipped historical AI-review deployment does not satisfy any step above.

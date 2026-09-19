# Coverage and function risk

The coverage gate measures behavior; it does not require an arbitrary aggregate
percentage. Run it after locked restore:

```powershell
pwsh -NoProfile -File scripts/check-coverage.ps1
```

The command runs Release tests with `coverage.runsettings`, finds the one
Cobertura report, evaluates every measured function against
`scripts/function-risk-baseline.json`, and writes these CI-ready files under
`artifacts/coverage`:

- `coverage.cobertura.xml` for line and branch publishing;
- `function-risk.json` with every measured function; and
- `function-risk.md` with the 25 worst CRAP scores.

`TickDown.Core` is included as a production assembly. The WinUI application
cannot be loaded as an ordinary test assembly, so the collector also instruments
only the actual `TickDown.ViewModels.*` and `TickDown.Services.SettingsService`
source files linked into the test project. Test namespaces and presentation test
doubles are not included. Generated-code and explicit coverage-exclusion
attributes plus generated XAML files are excluded. Async compiler state-machine
`MoveNext` bodies are retained and mapped through `AsyncStateMachineAttribute` to
unique source signatures, including generic arity and parameter types. Reported
compiler-generated callback and local-function bodies are retained instead of
being filtered with their closure classes. Callback sequence points that Coverlet
folds into their containing function remain part of that function's line rate.
The required Core, ViewModels, and Services source prefixes make an accidentally
empty or narrowed report fail.

## CRAP calculation

For each function:

```text
CRAP = complexity² × (1 - coverage)³ + complexity
```

Cyclomatic complexity comes from Cobertura. For a function with measured
branches, coverage is the lower of its branch and line rates. This keeps branch
coverage authoritative without letting uncovered callback sequence points folded
into the containing method disappear behind a covered outer branch. Functions
without branches use line coverage as the documented fallback. The checked-in
test fixture confirms that complexity 6 with zero branch coverage produces CRAP
42 for both `Stop` and `Tick`, matching the audit calculation.

The current focused lifecycle tests cover `CountdownTimer.Stop` and every branch
of `CountdownTimer.Tick`; their scores are now 1 and 6. The gate deliberately
retains existing higher-risk application functions in the baseline rather than
hiding them behind an aggregate percentage.

## Non-regression policy

- A new function fails when its CRAP score is greater than 30.
- An existing function fails when its score exceeds its checked-in baseline by
  more than 0.01, allowing only rounding noise.
- Improved scores pass without forcing baseline churn. A missing baseline source
  or function fails so collector narrowing cannot look like code deletion; a real
  removal requires the same reviewed baseline update as any contract change.
- Updating the baseline requires a reviewed explanation and this explicit command:

  ```powershell
  pwsh -NoProfile -File scripts/check-coverage.ps1 -UpdateBaseline
  ```

Never update the baseline merely to accept a regression. Add focused behavior
coverage or reduce complexity, then inspect both function-risk reports before
review. `scripts/tests/CoverageQuality.Tests.ps1` proves the formula, branch/line
floor, line fallback, generated-callback retention, unique async identities,
new-function threshold, report generation, and a deliberately uncovered branch
regression.

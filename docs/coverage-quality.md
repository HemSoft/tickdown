# Coverage and function risk

The coverage gate measures behavior; it does not require an arbitrary aggregate
percentage. Run it after locked restore:

```powershell
pwsh -NoProfile -File scripts/check-coverage.ps1
```

`-ResultsDirectory` is accepted only for the owned `artifacts/coverage` directory
or one of its descendants. The runner rejects shared artifact directories and
repository paths before performing cleanup.

The command runs Release tests with `coverage.runsettings`, finds the one
Cobertura report, evaluates every measured function against
`scripts/function-risk-baseline.json`, and writes these CI-ready files under
`artifacts/coverage`. Assembly metadata is resolved from the exact Release
`TargetPath` produced for the current test project, so stale framework or RID
outputs elsewhere under `bin` cannot change the result:

- `coverage.cobertura.xml` for line and branch publishing;
- `function-risk.json` with every measured function; and
- `function-risk.md` with the 25 worst CRAP scores.

The runner queries MSBuild for the x64 Release app project's transitive
`ProjectReference` closure and creates effective collector filters for every
resulting production assembly. It builds that app candidate, stages its managed
output beside the current test assembly, and a coverage-only test explicitly loads
every discovered candidate so even otherwise-unused project modules are instrumented.
The two current ViewModel files and SettingsService file are linked explicitly into
the test assembly and compiled with the x64 Release app's effective preprocessor
symbols rather than duplicating their unexecuted app copies. Coverlet requires a
trailing wildcard to collect each linked outer type and its generated nested types;
a reflection guard rejects any sibling that also matches one of those prefixes
before the report is accepted. The app exclusions match each exact type plus its
`/`-delimited generated nested types; they do not use sibling-matching prefixes. A new ViewModel or service type,
including one whose name starts with an existing type name, is measured from the
app candidate by default. Before collection, the runner scans the entire `src`
tree with Roslyn using the x64 Release app's effective preprocessor symbols. It
composes all block/file-scoped namespace ancestors plus containing types and the
identifier for each partial declaration from Roslyn token values, so verbatim and
escaped identifiers normalize to their metadata names, then
requires each excluded identity to come from its exact linked file. Test namespaces,
presentation doubles, and the two exact package-generated bootstrap types are not
included; there is no namespace-wide `Microsoft.*` exclusion. Only generated output
beneath `obj` is excluded by file path; every hand-written C# file under `src`,
including `*.g.cs`, remains measured. The runner parses active C# attribute syntax
and rejects `ExcludeFromCodeCoverage` and Coverlet's equivalent without mistaking
comments or strings for attributes; local and cross-file global aliases plus
verbatim/escaped identifiers are resolved as well. It independently
inspects the production assemblies and the three exact source-linked test types
for assembly, type, and member metadata while ignoring generated test-host
scaffolding. An exclusion is trusted only on one of the explicit current
RelayCommand properties carrying the exact CommunityToolkit generator identity;
property names and user-controlled generated markers cannot expand that allowlist. Async, iterator,
and async-iterator state-machine bodies, including local-function and lambda state
machines whose generated names use a different shape, are retained and mapped
from every covered assembly through the corresponding state-machine attribute to unique source
signatures. All source-bearing helper methods in a state machine, including
iterator `finally` bodies, are aggregated into that source function's complexity
and coverage. Reflection records zero as well as nonzero generic arity for
every ordinary method, so generic and nongeneric overloads sharing a parameter
signature remain distinct. Function IDs and every reflection lookup key begin with
the Cobertura assembly name; baseline schema version 3 therefore cannot collide
when separate production projects reuse a fully qualified type and method name.
Duplicate IDs within one assembly fail rather than overwrite baseline data.
User-defined regular and checked conversions bypass generic-occurrence matching and carry reflected target types, so legal target
overloads cannot share a baseline key. Reported compiler-generated callback and
local-function bodies are retained instead of being filtered with their closure classes. Callback sequence points that Coverlet
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

- A new function fails immediately when its CRAP score is greater than 30. A
  lower-risk new function still requires an explicit reviewed baseline update so
  later regression or removal cannot disappear from the inventory.
- An existing function fails when its score exceeds its checked-in baseline by
  more than 0.01, allowing only rounding noise.
- Improved scores pass without forcing baseline churn. A missing baseline source
  or function fails so collector narrowing cannot look like code deletion; a real
  removal requires the same reviewed baseline update as any contract change.
- Updating the baseline requires a reviewed explanation. Update mode permits
  below-threshold new functions but still rejects high-risk additions, existing
  regressions, and missing sources/functions before writing:

  ```powershell
  pwsh -NoProfile -File scripts/check-coverage.ps1 -UpdateBaseline
  ```

Never update the baseline merely to accept a regression. Add focused behavior
coverage or reduce complexity, then inspect both function-risk reports before
review. `scripts/tests/CoverageQuality.Tests.ps1` proves the formula, branch/line
floor, line fallback, generated-callback retention, unique async and iterator identities,
new-function threshold and guarded inventory updates, source/compiled exclusion checks, report generation,
and a deliberately uncovered branch regression.

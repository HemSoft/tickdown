# C# style policy

`.editorconfig` is the editor and Roslyn policy. `stylecop.json` supplies the
matching StyleCop settings. They intentionally agree on these conventions:

- `using` directives live inside file-scoped namespaces;
- `System` directives come first, without blank groups; and
- instance fields, properties, methods and events use `this.` qualification.

The qualification rule follows the established source convention and StyleCop
SA1101. Changing it requires updating both the declared policy and all source in
one reviewed pull request; do not alternate formatter output between tools.

XAML event handlers must retain the delegate signature expected by generated
WinUI code. When a required event argument is otherwise unused, the handler
validates the non-null framework contract with `ArgumentNullException.ThrowIfNull`
instead of removing the parameter, inventing a suppression, or using a discard
name that conflicts with StyleCop naming rules.

Run the same gates before review:

```powershell
pwsh -NoProfile -File scripts/tests/StylePolicy.Tests.ps1
dotnet format style --verify-no-changes
dotnet format whitespace --verify-no-changes
dotnet format analyzers --verify-no-changes
dotnet build src/TickDown.csproj --configuration Debug
dotnet build src/TickDown.csproj --configuration Release
```

All commands must pass on the same revision and pinned SDK. Analyzer severities
and quality thresholds stay enabled; formatting failures are fixed in source or
by reconciling contradictory policy.

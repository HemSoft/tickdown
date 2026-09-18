# Test boundaries

Run `dotnet test TickDown.sln --configuration Release` on Windows.

The unit project references the production Core assembly and compiles the actual
view-model source files with the same MVVM source generator as the application.
The persistence tests exercise generated commands and capture serialized models
at the `ISettingsService` boundary. They do not duplicate view-model logic.

`TickDown.WinUI.TestDoubles` provides only an inline dispatcher and color-holding
brushes, allowing those command tests to run without launching WinUI. Its folder
structure matches its `Microsoft.UI` namespace. The app never references this
assembly. These doubles cannot verify native dispatch, rendering, bindings,
accessibility, or audio; test those paths in an isolated real application profile.

The persistence suite checks rename, individual duration components, parsed time,
Quick Set and target-date commands. Every recorded snapshot must contain the final
model value, not just the final snapshot after a later edit.

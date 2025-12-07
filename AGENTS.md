# Rules for AI Agents

1. **No Pragma Exemptions**: Do not use `#pragma warning disable` to suppress build warnings or errors. Fix the underlying issue or configure the analyzer properly at the project level (e.g., `.editorconfig`) if it is a false positive.
2. **No Project File Suppressions**: Do not use `<NoWarn>` in `.csproj` files to suppress warnings (e.g., `<NoWarn>$(NoWarn);CS1591</NoWarn>`). Fix the underlying issue or configure the analyzer properly.
3. **No Attribute Suppressions**: Do not use `[SuppressMessage]` attributes to suppress warnings. Fix the underlying issue by improving the code structure or logic.
4. **No Disabling Analysis**: Do not disable or turn off any analysis that would compromise the quality or security of the project. Comply with warnings by fixing the code (e.g., adding XML comments for CS1591).

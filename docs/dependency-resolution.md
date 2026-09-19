# Dependency resolution

TickDown requires .NET SDK 10.0.108. `global.json` uses `rollForward: disable`,
so a missing SDK fails instead of silently selecting another feature band or
patch. Install that SDK before restoring. The repository also pins analyzer
behavior to .NET 10 recommended rules rather than the moving `latest` policy.
See Microsoft's [`global.json` selection rules](https://learn.microsoft.com/dotnet/core/tools/global-json).

Every NuGet `PackageReference` uses NuGet's exact closed-range syntax, such as
`Version="[8.4.2]"`; a bare `8.4.2` means “8.4.2 or later” and is not accepted.
Each project checks in a `packages.lock.json` file with direct and transitive
versions plus content hashes.
Restore with:

```powershell
dotnet restore TickDown.sln --locked-mode
```

Locked mode fails when a project and its lock disagree. It does not rewrite the
reviewed resolution. npm tools follow the same rule through `npm ci` and the
checked-in `package-lock.json`. See NuGet's [dependency locking guidance](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#locking-dependencies).

## Reviewing an upgrade

Dependabot checks NuGet, npm and pinned GitHub Actions every Monday morning in
`America/New_York`. Compatible minor and patch updates are grouped; major updates
remain separate because they need their own compatibility decision. HemSoft is
the requested reviewer. Dependabot pull requests must pass the same required
Windows workflow as other changes.

For a manual NuGet upgrade:

1. Change exact closed-range top-level versions in the project or shared props file.
2. Run `dotnet restore TickDown.sln --force-evaluate` to regenerate every affected
   lock file.
3. Review direct and transitive lock diffs. Keep security advisories separate from
   “newer version available” findings.
4. Run `scripts/tests/DependencyResolution.Tests.ps1`, the Release build, all
   tests, x64/x86/ARM64 application builds, and vulnerability scans.
5. Explain compatibility or migration work in the pull request. Never edit a lock
   file by hand to make locked restore pass.

Repository Dependabot security updates must remain enabled. The checked-in
configuration supplies the review schedule; the live repository setting allows
GitHub to open urgent security updates outside routine version review. Changing
that live setting requires a reviewed issue and restoration plan.

## Clean-machine proof

To compare two clean restores, copy the tracked tree twice without `bin`, `obj`
or `node_modules`. Give each copy an empty `NUGET_PACKAGES` directory, run locked
restore with `--no-cache`, and compare the four lock files and resolved package
identities. The same commit must produce the same identities. Build output bytes
are not claimed reproducible because compiler paths, signing and Windows tooling
may still encode environment data.

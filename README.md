# tickdown

A Windows desktop countdown timer built with .NET 10 and WinUI 3.

## Local launch

Run `./run.ps1` from PowerShell. The script builds when the selected output is
missing or an application source, project, dependency lock, XAML, asset,
manifest, or repository build setting has changed. Unchanged repeat launches
skip build and restore. Use `./run.ps1 -Configuration Release` when a Release
launch is needed.

## Isolated validation

Set `TICKDOWN_SETTINGS_DIRECTORY` to a disposable directory before launching
when testing. The application uses that directory for both timer and window
settings instead of your normal profile.

```powershell
$env:TICKDOWN_SETTINGS_DIRECTORY = Join-Path $env:TEMP ([guid]::NewGuid().ToString())
./run.ps1
```

Pausing freezes the remaining countdown. Starting a paused timer resumes that
remaining time; starting a completed timer begins its full configured duration.
Stop preserves remaining time, while Reset restores the full duration.

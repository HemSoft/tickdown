# tickdown

A Windows desktop countdown timer built with .NET 10 and WinUI 3.

## Isolated validation

Set `TICKDOWN_SETTINGS_DIRECTORY` to a disposable directory before launching
when testing. The application uses that directory for both timer and window
settings instead of your normal profile.

```powershell
$env:TICKDOWN_SETTINGS_DIRECTORY = Join-Path $env:TEMP ([guid]::NewGuid().ToString())
dotnet run --project src/TickDown.csproj --launch-profile 'TickDown (Unpackaged)'
```

Pausing freezes the remaining countdown. Starting a paused timer resumes that
remaining time; starting a completed timer begins its full configured duration.
Stop preserves remaining time, while Reset restores the full duration.

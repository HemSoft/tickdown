#Requires -Version 7.4
[CmdletBinding()]
param(
    [ValidateSet('Fast', 'Full')][string]$Mode = 'Fast',
    [ValidatePattern('^[A-Za-z0-9._-]+$')][string]$RunLabel = 'local',
    [string]$OutputDirectory = 'artifacts/desktop-qualification',
    [switch]$CaptureEvidence,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Resolve-Path "$PSScriptRoot/.."
Import-Module (Join-Path $PSScriptRoot 'DesktopQualification.psm1') -Force
$ownedOutput = Resolve-DesktopQualificationOutputPath $root $OutputDirectory
$policy = Get-Content (Join-Path $PSScriptRoot 'desktop-qualification-policy.json') -Raw | ConvertFrom-Json
$runPolicy = if ($Mode -eq 'Full') { $policy.full } else { $policy.fast }
$candidate = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the candidate revision.' }
$dirty = (& git -C $root status --porcelain --untracked-files=all | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the candidate working tree.' }
if (!$AllowDirty -and $dirty) { throw "Desktop qualification requires a clean candidate tree:`n$dirty" }
if ($env:GITHUB_SHA -and $env:GITHUB_SHA -ne $candidate) { throw "Candidate $candidate does not match GITHUB_SHA $env:GITHUB_SHA." }

$runDirectory = Join-Path $ownedOutput "$candidate/$RunLabel"
if (Test-Path $runDirectory) { Remove-Item $runDirectory -Recurse -Force }
New-Item $runDirectory -ItemType Directory -Force | Out-Null
$profile = Join-Path $runDirectory 'profile'
$probe = Join-Path $runDirectory 'probe'
New-Item $profile, $probe -ItemType Directory -Force | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing.Common
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class QualificationWindowNative {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
'@

function New-PropertyCondition($Property, $Value) {
    New-Object System.Windows.Automation.PropertyCondition($Property, $Value)
}

function Get-AppWindow([int]$ProcessId, [int]$TimeoutSeconds = 15) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $condition = New-PropertyCondition ([System.Windows.Automation.AutomationElement]::ProcessIdProperty) $ProcessId
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($null -ne $window) { return $window }
        Start-Sleep -Milliseconds 100
    }
    throw "Application window did not appear for process $ProcessId."
}

function Get-ElementsByType($Window, $ControlType) {
    $condition = New-PropertyCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) $ControlType
    return @($Window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition))
}

function Get-ElementsByName($Window, [string]$Name) {
    $condition = New-PropertyCondition ([System.Windows.Automation.AutomationElement]::NameProperty) $Name
    return @($Window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition))
}

function Get-ElementsByAutomationId($Window, [string]$AutomationId) {
    $condition = New-PropertyCondition ([System.Windows.Automation.AutomationElement]::AutomationIdProperty) $AutomationId
    return @($Window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition))
}

function Invoke-Element($Element) {
    $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Invoke-LastByAutomationId($Window, [string]$AutomationId) {
    $elements = @(Get-ElementsByAutomationId $Window $AutomationId)
    if ($elements.Count -eq 0) { throw "Action not found: $AutomationId" }
    Invoke-Element $elements[-1]
}

function Get-ElementValue($Element) {
    $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
}

function Set-ElementValue($Element, [string]$Value) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $Element.SetFocus()
    $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
    while ($timer.Elapsed.TotalMilliseconds -lt 2000) {
        if ((Get-ElementValue $Element) -eq $Value) { return $timer.Elapsed.TotalMilliseconds }
        Start-Sleep -Milliseconds 10
    }
    throw "Editable control did not report '$Value'; observed '$(Get-ElementValue $Element)'."
}

function Wait-Until([scriptblock]$Condition, [double]$TimeoutSeconds, [string]$Failure) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 50
    }
    throw $Failure
}

function Get-TimeEditors($Window) {
    @(Get-ElementsByAutomationId $Window 'TimerDurationEditor')
}

function Get-NameEditors($Window) {
    @(Get-ElementsByAutomationId $Window 'TimerNameEditor')
}

function Request-Snapshot([long]$RequestId, [switch]$ResetLatencyWindow) {
    $requestPath = Join-Path $probe 'request.json'
    $temporaryPath = $requestPath + '.tmp'
    [ordered]@{ RequestId = $RequestId; ResetLatencyWindow = [bool]$ResetLatencyWindow } |
        ConvertTo-Json | Set-Content $temporaryPath -Encoding utf8
    Move-Item $temporaryPath $requestPath -Force
    $responsePath = Join-Path $probe "snapshot-$RequestId.json"
    Wait-Until { Test-Path $responsePath -PathType Leaf } 10 "Snapshot $RequestId was not produced."
    $snapshot = Get-Content $responsePath -Raw | ConvertFrom-Json
    if ($snapshot.Candidate -ne $candidate) { throw "Snapshot candidate $($snapshot.Candidate) does not match $candidate." }
    if ([IO.Path]::GetFullPath($snapshot.SettingsDirectory) -ne [IO.Path]::GetFullPath($profile)) { throw 'Snapshot did not use the disposable settings profile.' }
    return $snapshot
}

function Capture-Window($Window, [string]$Name) {
    $bounds = $Window.Current.BoundingRectangle
    $display = $policy.display
    if ($bounds.Left -lt $display.x -or $bounds.Top -lt $display.y -or $bounds.Right -gt ($display.x + $display.width) -or $bounds.Bottom -gt ($display.y + $display.height)) {
        throw "Window escaped DISPLAY1: $bounds"
    }
    $bitmap = New-Object System.Drawing.Bitmap ([int]$bounds.Width), ([int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.Left, [int]$bounds.Top, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $runDirectory "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Start-EvidenceRecording {
    if (!$CaptureEvidence) { return $null }
    $ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
    $recordingPath = Join-Path $runDirectory 'interaction.mp4'
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $ffmpeg
    $startInfo.Arguments = "-y -f gdigrab -framerate 12 -offset_x $($policy.display.x) -offset_y $($policy.display.y) -video_size $($policy.display.width)x$($policy.display.height) -i desktop -c:v libx264 -preset ultrafast -pix_fmt yuv420p `"$recordingPath`""
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $process = [Diagnostics.Process]::Start($startInfo)
    Start-Sleep -Seconds 1
    return $process
}

function Stop-EvidenceRecording($Recorder) {
    if ($null -eq $Recorder) { return }
    $Recorder.StandardInput.WriteLine('q')
    if (!$Recorder.WaitForExit(15000)) { $Recorder.Kill($true) }
    $Recorder.Dispose()
}

function Add-OneSecondTimer($Window, [string]$Name, [Collections.Generic.List[double]]$InputLatencies) {
    $previousCount = @(Get-TimeEditors $Window).Count
    Invoke-LastByAutomationId $Window 'AddTimerButton'
    Wait-Until { (Get-TimeEditors $Window).Count -gt $previousCount -and (Get-NameEditors $Window).Count -gt $previousCount } 3 'Added timer did not appear.'
    $timeEditors = @(Get-TimeEditors $Window)
    $nameEditors = @(Get-NameEditors $Window)
    $timeEditor = $timeEditors[-1]
    $nameEditor = $nameEditors[-1]
    if ($null -eq $timeEditor -or $null -eq $nameEditor) { throw "Added timer editors were not stable: time=$($timeEditors.Count), name=$($nameEditors.Count)." }
    $InputLatencies.Add((Set-ElementValue $nameEditor $Name))
    $InputLatencies.Add((Set-ElementValue $timeEditor '00:00:01'))
    Invoke-LastByAutomationId $Window 'StartTimerButton'
    Wait-Until { (Get-ElementsByAutomationId $Window 'DismissTimerButton').Count -gt 0 } 4 "Timer $Name did not complete."
    Invoke-LastByAutomationId $Window 'DismissTimerButton'
    Invoke-LastByAutomationId $Window 'RemoveTimerButton'
}

function Add-LoadTimer($Window, [string]$Name, [Collections.Generic.List[double]]$InputLatencies) {
    $previousCount = @(Get-TimeEditors $Window).Count
    Invoke-LastByAutomationId $Window 'AddTimerButton'
    Wait-Until { (Get-TimeEditors $Window).Count -gt $previousCount -and (Get-NameEditors $Window).Count -gt $previousCount } 3 'Load timer did not appear.'
    $timeEditors = @(Get-TimeEditors $Window)
    $nameEditors = @(Get-NameEditors $Window)
    $timeEditor = $timeEditors[-1]
    $nameEditor = $nameEditors[-1]
    if ($null -eq $timeEditor -or $null -eq $nameEditor) { throw "Load timer editors were not stable: time=$($timeEditors.Count), name=$($nameEditors.Count)." }
    $InputLatencies.Add((Set-ElementValue $nameEditor $Name))
    $InputLatencies.Add((Set-ElementValue $timeEditor '00:00:20'))
}

function Select-Theme($Window, [string]$Theme) {
    $comboBoxes = @(Get-ElementsByType $Window ([System.Windows.Automation.ControlType]::ComboBox))
    if ($comboBoxes.Count -eq 0) { throw 'Theme selector was not exposed to UI Automation.' }
    $combo = $comboBoxes[0]
    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Wait-Until { (Get-ElementsByName $Window $Theme).Count -gt 0 } 2 "Theme item $Theme did not appear."
    $selectionPattern = $null
    foreach ($item in @(Get-ElementsByName $Window $Theme)) {
        if ($item.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selectionPattern)) {
            $selectionPattern.Select()
            Start-Sleep -Milliseconds 300
            return
        }
    }
    throw "Theme item $Theme did not expose a selection pattern."
}

$timerData = @(
    [ordered]@{
        Duration = '00:00:03'; Remaining = '00:00:03'; State = 0; Name = 'Qualification recovery'
        EnableAlarm = $true; AlarmSound = 'Alarm 01'; EnableAlarmRepeat = $true
        AlarmRepeatIntervalSeconds = 5; AlarmExpirationMinutes = 1
    }
) | ConvertTo-Json -Depth 5 -AsArray
Set-Content (Join-Path $profile 'timers.json') '{ invalid recovery candidate' -Encoding utf8
Set-Content (Join-Path $profile 'timers.json.bak') $timerData -Encoding utf8
[ordered]@{ X = 3520; Y = 100; Width = 900; Height = 720; Theme = 'Light'; IsMaximized = $false } |
    ConvertTo-Json | Set-Content (Join-Path $profile 'window.json') -Encoding utf8

$buildOutput = (& dotnet build (Join-Path $root 'src/TickDown.csproj') -c Release --no-restore -p:Platform=x64 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw "Qualification app build failed:`n$buildOutput" }
$targetOutput = (& dotnet msbuild (Join-Path $root 'src/TickDown.csproj') -getProperty:TargetPath -p:Configuration=Release -p:Platform=x64 -nologo 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not resolve app target:`n$targetOutput" }
$appDll = @($targetOutput -split "`r?`n" | Where-Object { $_.Trim() })[-1].Trim()
$appExecutable = [IO.Path]::ChangeExtension($appDll, '.exe')
if (!(Test-Path $appExecutable -PathType Leaf)) { throw "Qualification executable not found: $appExecutable" }

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$inputLatencies = [Collections.Generic.List[double]]::new()
$requestId = 0L
$app = $null
$recorder = $null
try {
    $app = Start-Process $appExecutable -Environment @{
        TICKDOWN_SETTINGS_DIRECTORY = $profile
        TICKDOWN_QUALIFICATION_DIRECTORY = $probe
        TICKDOWN_QUALIFICATION_CANDIDATE = $candidate
    } -PassThru
    $null = $app.WaitForInputIdle(10000)
    $window = Get-AppWindow $app.Id
    if (![QualificationWindowNative]::SetWindowPos($app.MainWindowHandle, [IntPtr]::Zero, 3520, 100, 900, 720, 0x0040)) {
        throw 'Could not place qualification window on DISPLAY1.'
    }
    Start-Sleep -Milliseconds 500
    $bounds = $window.Current.BoundingRectangle
    if ($bounds.Left -lt $policy.display.x -or $bounds.Right -gt ($policy.display.x + $policy.display.width)) { throw "Window is not confined to DISPLAY1: left=$($bounds.Left), top=$($bounds.Top), right=$($bounds.Right), bottom=$($bounds.Bottom)" }
    $recorder = Start-EvidenceRecording

    Wait-Until { (Get-TimeEditors $window).Count -eq 1 } 5 'Recovered timer did not load from backup.'
    if (!(Get-ChildItem $profile -Filter 'timers.json.corrupt.*' -ErrorAction SilentlyContinue)) { throw 'Corrupt settings were not archived during recovery.' }
    $nameEditor = @(Get-NameEditors $window)[0]
    $inputLatencies.Add((Set-ElementValue $nameEditor 'Qualification journey'))
    $nameEditor.SetFocus()
    if (!$nameEditor.Current.HasKeyboardFocus) { throw 'Timer name did not receive keyboard focus.' }
    [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
    Start-Sleep -Milliseconds 100
    $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
    if ($null -eq $focused -or [string]::IsNullOrWhiteSpace($focused.Current.Name)) { throw 'Keyboard tab navigation did not reach a named control.' }

    $actionableTypes = [System.Windows.Automation.ControlType[]]@(
        [System.Windows.Automation.ControlType]::Button,
        [System.Windows.Automation.ControlType]::Edit,
        [System.Windows.Automation.ControlType]::ComboBox,
        [System.Windows.Automation.ControlType]::CheckBox
    )
    $unnamed = @()
    foreach ($type in $actionableTypes) {
        $unnamed += @(Get-ElementsByType $window $type | Where-Object { $_.Current.IsEnabled -and [string]::IsNullOrWhiteSpace($_.Current.Name) })
    }
    if ($unnamed.Count -gt 0) {
        $unnamedDetails = @($unnamed | ForEach-Object { "$($_.Current.ControlType.ProgrammaticName)/$($_.Current.AutomationId)" }) -join ', '
        throw "$($unnamed.Count) enabled actionable controls have no accessible name: $unnamedDetails"
    }

    Capture-Window $window 'journey-light'
    Invoke-LastByAutomationId $window 'StartTimerButton'
    Start-Sleep -Milliseconds 500
    Invoke-LastByAutomationId $window 'PauseTimerButton'
    $pausedValue = Get-ElementValue (@(Get-TimeEditors $window)[0])
    Start-Sleep -Milliseconds 600
    if ((Get-ElementValue (@(Get-TimeEditors $window)[0])) -ne $pausedValue) { throw 'Paused display continued to change.' }
    Invoke-LastByAutomationId $window 'StartTimerButton'
    Wait-Until { (Get-ElementsByAutomationId $window 'DismissTimerButton').Count -eq 1 } 5 'Recovered timer did not complete.'
    $alarmSnapshot = Request-Snapshot (++$requestId)
    if ($alarmSnapshot.Runtime.ActiveAlarmRepeatTimers -ne 1 -or $alarmSnapshot.Runtime.ActiveMediaPlayers -ne 1) { throw 'Alarm repeat and native playback were not active after completion.' }
    Start-Sleep -Milliseconds 5200
    Invoke-LastByAutomationId $window 'DismissTimerButton'
    $alarmCleanup = Request-Snapshot (++$requestId)
    if ($alarmCleanup.Runtime.ActiveAlarmRepeatTimers -ne 0 -or $alarmCleanup.Runtime.ActiveMediaPlayers -ne 0) { throw 'Dismiss did not release alarm-repeat resources.' }
    Invoke-LastByAutomationId $window 'StartTimerButton'
    Start-Sleep -Milliseconds 300
    Invoke-LastByAutomationId $window 'StopTimerButton'
    Invoke-LastByAutomationId $window 'RemoveTimerButton'

    Select-Theme $window 'Dark'
    Capture-Window $window 'journey-dark'
    Select-Theme $window 'Light'
    $window.SetFocus()
    Start-Sleep -Milliseconds 100
    [System.Windows.Forms.SendKeys]::SendWait('^=')
    Start-Sleep -Milliseconds 300
    $zoomed = Request-Snapshot (++$requestId)
    if ($zoomed.ZoomFactor -le 1.0) { throw "Keyboard zoom did not increase the zoom factor: $($zoomed.ZoomFactor)" }
    $window.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('^0')

    for ($cycle = 0; $cycle -lt [int]$runPolicy.warmupCycles; $cycle++) {
        Add-OneSecondTimer $window "Warmup $cycle" $inputLatencies
    }
    $resourceBefore = Request-Snapshot (++$requestId) -ResetLatencyWindow

    for ($cycle = 0; $cycle -lt [int]$runPolicy.measuredCycles; $cycle++) {
        Add-OneSecondTimer $window "Measured $cycle" $inputLatencies
    }

    for ($index = 0; $index -lt [int]$runPolicy.loadTimerCount; $index++) {
        Add-LoadTimer $window "Load $index" $inputLatencies
    }
    foreach ($start in @(Get-ElementsByAutomationId $window 'StartTimerButton')) { Invoke-Element $start }
    $null = Request-Snapshot (++$requestId) -ResetLatencyWindow
    Start-Sleep -Seconds ([double]$runPolicy.loadSeconds)
    $loadSnapshot = Request-Snapshot (++$requestId)
    foreach ($stop in @(Get-ElementsByAutomationId $window 'StopTimerButton')) { Invoke-Element $stop }
    while ((Get-ElementsByAutomationId $window 'RemoveTimerButton').Count -gt 0) { Invoke-LastByAutomationId $window 'RemoveTimerButton' }
    Start-Sleep -Milliseconds 500
    $resourceAfter = Request-Snapshot (++$requestId)

    $completedCandidate = (& git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not revalidate the candidate revision.' }
    if ($completedCandidate -ne $candidate) { throw "Candidate changed during desktop qualification: $candidate -> $completedCandidate." }
    $completedDirty = (& git -C $root status --porcelain --untracked-files=all | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not revalidate the candidate working tree.' }
    if (!$AllowDirty -and $completedDirty) { throw "Working tree changed during desktop qualification:`n$completedDirty" }

    $highContrastResource = (Get-Content (Join-Path $root 'src/Views/MainPage.xaml') -Raw).Contains('<ResourceDictionary x:Key="HighContrast">')
    $evaluation = Get-QualificationEvaluation $resourceBefore $resourceAfter $loadSnapshot $inputLatencies.ToArray() $policy.budgets ([int]$runPolicy.loadTimerCount) ([double]$runPolicy.loadSeconds) -HighContrastResource $highContrastResource
    $result = [pscustomobject]@{
        Candidate = $candidate
        Mode = $Mode
        RunLabel = $RunLabel
        ElapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
        WorkingTreeClean = !$dirty -and !$completedDirty
        IsolatedSettingsDirectory = $profile
        Machine = [pscustomobject]@{
            ProcessorCount = $resourceAfter.ProcessorCount
            TotalAvailableMemoryBytes = $resourceAfter.TotalAvailableMemoryBytes
            OperatingSystem = [Environment]::OSVersion.VersionString
        }
        Workload = [pscustomobject]@{
            WarmupCycles = [int]$runPolicy.warmupCycles
            MeasuredCycles = [int]$runPolicy.measuredCycles
            LoadTimerCount = [int]$runPolicy.loadTimerCount
            LoadSeconds = [double]$runPolicy.loadSeconds
        }
        Accessibility = [pscustomobject]@{
            NamedActionableControls = $true
            KeyboardFocusAndTab = $true
            LightTheme = $true
            DarkTheme = $true
            HighContrastResource = $highContrastResource
            SystemHighContrastEnabled = [System.Windows.Forms.SystemInformation]::HighContrast
            ZoomFactorObserved = $zoomed.ZoomFactor
        }
        Recovery = [pscustomobject]@{ BackupLoaded = $true; CorruptPrimaryArchived = $true }
        Alarm = [pscustomobject]@{ RepeatObserved = $true; CleanupObserved = $true }
        Evaluation = $evaluation
        Snapshots = [pscustomobject]@{ ResourceBefore = $resourceBefore; Load = $loadSnapshot; ResourceAfter = $resourceAfter }
    }
    Write-DesktopQualificationReport $result $runDirectory
    if (!$evaluation.Passed) { throw "Desktop qualification failed:`n- $($evaluation.Failures -join "`n- ")" }
    "Desktop qualification passed for $candidate ($Mode/$RunLabel) in $($result.ElapsedSeconds) seconds."
    "Artifacts: $runDirectory"
}
finally {
    Stop-EvidenceRecording $recorder
    if ($null -ne $app -and !$app.HasExited) {
        $null = $app.CloseMainWindow()
        if (!$app.WaitForExit(10000)) { $app.Kill($true) }
    }
    if ($null -ne $app) { $app.Dispose() }
}

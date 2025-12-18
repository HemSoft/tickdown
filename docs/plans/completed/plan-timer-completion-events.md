# Timer Completion Events Feature Plan

## Overview

Add configurable events when a timer hits zero: visual color change and audible alarm with repeat capability.

## Features

### 1. Completion Color Change

- Timer box background changes to a configurable color when timer completes
- Default: Green (if not configured)
- Color persists until user resets timer or sets a new time
- Predefined colors: Green, Red, Blue, Yellow, Orange, Purple
- Full color picker available for custom colors
- Colors must work well in both light and dark themes

### 2. Audible Alarm

- Play a Windows system sound when timer completes
- Sound selection dialog with preview button
- Available system sounds: Asterisk, Beep, Exclamation, Hand, Question, etc.
- Default: Standard alarm/Asterisk sound

### 3. Alarm Repeat

- Option to repeat alarm at configurable intervals
- Default grace period: 10 seconds
- Configurable expiration time (alarm stops repeating after this duration)
- Default expiration: 30 minutes
- Alarm stops when user acknowledges/resets timer

### 4. Dismiss/Acknowledge

- Add dismiss button on completed timers to stop alarm and clear color
- Alternatively, Reset/Stop actions also dismiss the completion state

### 5. Light/Dark Mode Theme

- Theme toggle in top-right corner of UI
- Options: Light, Dark, System (follows Windows theme)
- Persisted as global app preference in window settings

---

## Model Changes

### CountdownTimer.cs

Add properties:

```csharp
public bool EnableCompletionColor { get; set; } = true;
public string CompletionColor { get; set; } = "#4CAF50"; // Green

public bool EnableAlarm { get; set; } = true;
public string AlarmSound { get; set; } = "Asterisk";

public bool EnableAlarmRepeat { get; set; } = false;
public int AlarmRepeatIntervalSeconds { get; set; } = 10;
public int AlarmExpirationMinutes { get; set; } = 30;
```

### WindowSettings.cs

Add property:

```csharp
public string Theme { get; set; } = "System"; // "Light", "Dark", "System"
```

---

## New Services

### IAudioService / AudioService

```csharp
public interface IAudioService
{
    IReadOnlyList<string> GetAvailableSystemSounds();
    void PlaySound(string soundName);
    void StopSound();
}
```

- Uses `System.Media.SystemSounds` for playback
- Provides list of available system sounds

### IThemeService / ThemeService

```csharp
public interface IThemeService
{
    string CurrentTheme { get; }
    event EventHandler? ThemeChanged;
    void SetTheme(string theme);
    void Initialize();
}
```

- Manages app theme switching
- Listens to Windows theme changes when set to "System"

---

## ViewModel Changes

### TimerViewModel.cs

Add properties:

```csharp
// Completion settings
public bool EnableCompletionColor { get; set; }
public string CompletionColor { get; set; }
public bool EnableAlarm { get; set; }
public string AlarmSound { get; set; }
public bool EnableAlarmRepeat { get; set; }
public int AlarmRepeatIntervalSeconds { get; set; }
public int AlarmExpirationMinutes { get; set; }

// Runtime state
public bool IsCompleted { get; }
public Brush CompletionBackgroundBrush { get; }
```

Add methods:

```csharp
private void OnTimerCompleted(); // Triggers color + alarm
private void StartAlarmRepeat();
private void StopAlarmRepeat();

[RelayCommand]
private void Dismiss(); // Stops alarm, clears color, keeps timer at zero
```

### MainViewModel.cs

Add properties:

```csharp
public string CurrentTheme { get; set; }
public IReadOnlyList<string> AvailableThemes { get; }
```

Add commands:

```csharp
[RelayCommand]
private void SetTheme(string theme);
```

---

## View Changes

### MainPage.xaml

1. **Theme toggle** in header (top-right):
   - ComboBox or SegmentedControl with Light/Dark/System options

2. **Timer card updates**:
   - Bind `Background` to `CompletionBackgroundBrush`
   - Add "Dismiss" button (visible when `IsCompleted`)

3. **Completion settings section** (alongside Quick Set area):
   - Expander or section labeled "When Timer Completes"
   - Checkbox: Enable color change + Color picker button
   - Checkbox: Enable alarm + Sound selector button
   - Checkbox: Repeat alarm + Interval picker + Expiration picker

### New: SoundPickerDialog.xaml

- ListBox of available system sounds
- Preview button to play selected sound
- OK/Cancel buttons

### New: ColorPickerFlyout or inline ColorPicker

- Predefined color swatches (Green, Red, Blue, Yellow, Orange, Purple)
- "Custom..." button opens full ColorPicker

---

## File Changes Summary

| File | Action |
|------|--------|
| `src/TickDown.Core/Models/CountdownTimer.cs` | Add completion settings properties |
| `src/TickDown.Core/Models/WindowSettings.cs` | Add Theme property |
| `src/TickDown.Core/Services/IAudioService.cs` | New interface |
| `src/Services/AudioService.cs` | New implementation |
| `src/TickDown.Core/Services/IThemeService.cs` | New interface |
| `src/Services/ThemeService.cs` | New implementation |
| `src/Services/ServiceCollectionExtensions.cs` | Register new services |
| `src/ViewModels/TimerViewModel.cs` | Add completion properties, alarm logic, dismiss command |
| `src/ViewModels/MainViewModel.cs` | Add theme management |
| `src/Views/MainPage.xaml` | Theme toggle, completion settings UI, dismiss button |
| `src/Views/MainPage.xaml.cs` | Wire up theme changes |
| `src/Views/SoundPickerDialog.xaml` | New dialog |
| `src/Views/SoundPickerDialog.xaml.cs` | New code-behind |
| `src/App.xaml.cs` | Initialize theme on startup |
| `src/Converters/BoolToVisibilityConverter.cs` | Possibly extend or add new converters |

---

## Implementation Order

1. **Theme system** - WindowSettings, ThemeService, UI toggle
2. **Model updates** - CountdownTimer completion properties
3. **Audio service** - IAudioService, AudioService, SoundPickerDialog
4. **Timer completion logic** - ViewModel changes for color/alarm/repeat
5. **UI updates** - Completion settings section, dismiss button, background binding
6. **Persistence** - Ensure new properties serialize/deserialize correctly
7. **Testing** - Manual verification of all scenarios

---

## Default Values

| Setting | Default |
|---------|---------|
| Enable completion color | true |
| Completion color | Green (#4CAF50) |
| Enable alarm | true |
| Alarm sound | Asterisk |
| Enable alarm repeat | false |
| Repeat interval | 10 seconds |
| Alarm expiration | 30 minutes |
| Theme | System |

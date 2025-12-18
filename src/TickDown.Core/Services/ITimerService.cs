// Copyright © 2025 HemSoft

namespace TickDown.Core.Services;

/// <summary>
/// Service interface for providing timer tick events.
/// </summary>
public interface ITimerService : IDisposable
{
    /// <summary>
    /// Occurs when the timer interval has elapsed.
    /// </summary>
    event EventHandler? Tick;
}
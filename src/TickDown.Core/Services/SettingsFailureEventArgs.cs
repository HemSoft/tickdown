// Copyright © 2025 HemSoft

namespace TickDown.Core.Services;

/// <summary>
/// Describes a settings failure or recovery that must be shown to the user.
/// </summary>
/// <param name="message">The user-facing description.</param>
/// <param name="exception">The underlying failure.</param>
/// <param name="isRecovered">Whether a valid backup was recovered.</param>
public sealed class SettingsFailureEventArgs(string message, Exception exception, bool isRecovered = false) : EventArgs
{
    /// <summary>
    /// Gets the user-facing description.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the underlying failure.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets a value indicating whether a valid backup was recovered.
    /// </summary>
    public bool IsRecovered { get; } = isRecovered;
}
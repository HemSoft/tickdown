// Copyright © 2025 HemSoft

namespace Microsoft.UI.Dispatching;

/// <summary>
/// Executes callbacks inline for view-model command tests, not native UI tests.
/// </summary>
public sealed class DispatcherQueue
{
    /// <summary>
    /// Gets the number of callbacks executed by this dispatcher.
    /// </summary>
    public int ExecutedCallbacks { get; private set; }

    /// <summary>
    /// Creates an inline test dispatcher.
    /// </summary>
    /// <returns>The inline dispatcher.</returns>
    public static DispatcherQueue GetForCurrentThread() => new();

    /// <summary>
    /// Runs the callback synchronously.
    /// </summary>
    /// <param name="action">The callback.</param>
    /// <returns>True after the callback completes.</returns>
    public bool TryEnqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        this.ExecutedCallbacks++;
        return true;
    }
}
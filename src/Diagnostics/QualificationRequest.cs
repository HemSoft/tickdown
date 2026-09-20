// Copyright © 2025 HemSoft

namespace TickDown.Diagnostics;

/// <summary>
/// Represents one snapshot request written by the qualification harness.
/// </summary>
internal sealed class QualificationRequest
{
    /// <summary>
    /// Gets the monotonically increasing request identifier.
    /// </summary>
    public long RequestId { get; init; }

    /// <summary>
    /// Gets a value indicating whether latency and throughput counters should reset after capture.
    /// </summary>
    public bool ResetLatencyWindow { get; init; }
}
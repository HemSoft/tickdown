# Timer duration input

Enter a nonnegative duration that fits both `TimeSpan` and a calendar deadline
relative to the current local clock. There is no fixed product cap in years.
A duration beyond `DateTime.MaxValue` is rejected even if `TimeSpan` can hold it.
Invalid input restores the previous duration and formatted display.

Unit input uses invariant decimal notation with a dot, independent of Windows
culture. Examples are `1.5h`, `10m`, `15sec`, and `2 hours`. Units are case-insensitive;
`h`, `hour`, `hours`, `m`, `min`, `s`, and `sec` are supported. A bare number means
minutes. Group separators, exponent notation, NaN, and infinity are not supported.

Time-span input uses the current culture through `TimeSpan.TryParse`, including
forms such as `01:02:03` and `1.02:03:04`. Fractional seconds are truncated to the
whole-second precision of the existing timer editor. Zero remains accepted;
starting a zero-duration timer retains the existing five-minute default.

After trimming outer whitespace, the parser rejects input longer than 64
characters before regular-expression matching. That limit exceeds every supported
canonical value that can produce a representable deadline and prevents an
unbounded paste from occupying the UI parser. A defensive regular-expression
timeout is also treated as ordinary invalid input.

Validation checks bounds before multiplication or conversion. Starting computes
and validates the deadline from one clock sample inside the timer model, so time
advancing after text validation cannot overflow startup. Deadline formatting has
its own calendar check. A loaded or formerly valid duration outside the current
calendar range therefore cannot overflow either path.

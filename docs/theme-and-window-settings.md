# Theme and window settings

TickDown stores theme, restored-window geometry and maximized state in one
`window.json` document. Geometry updates modify the loaded document instead of
constructing a replacement, so current and future unrelated preferences remain
unchanged.

On restored-window shutdown, TickDown records the current position and size. On
maximized shutdown, it marks the window maximized but retains the last restored
position and size. Light, Dark and System theme selections therefore survive
both shutdown paths.

The main view model is created before asynchronous theme initialization can
finish. Theme initialization raises `ThemeChanged` after loading and applying
the saved selection. The selector then reflects the same theme that the root
window renders. If the view model is created later, it reads the initialized
value directly.

Closing uses the ordered settings queue and waits for the final merged snapshot.
A persistence failure keeps the app open and shows the settings error rather
than silently replacing the saved document.

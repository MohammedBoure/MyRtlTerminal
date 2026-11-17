# Views

WPF windows, dialogs, and partial classes for the terminal UI.

## Files

- `MainWindow.xaml` / `MainWindow.xaml.cs` — Primary terminal window host, pane layout, input routing, and session lifecycle.
- `MainWindow.Commands.cs` — Built-in terminal command tracker and `help` command dispatcher exposing bar and session features via typed commands.
- `MainWindow.Suggestions.cs` — Floating command-history and built-in command autocomplete popup with Up/Down arrow navigation.
- `MainWindow.Bell.cs` — Terminal bell notification handling.
- `MainWindow.CloseConfirmation.cs` — Confirmation prompt when closing multiple active tabs.
- `MainWindow.Compatibility.cs` — Per-pane TUI compatibility profile selection.
- `MainWindow.DirectoryHistory.cs` — Directory observation, polling, and recent directory opening.
- `MainWindow.Language.cs` — Live UI language switching (`English`, `Persian`, `Arabic`).
- `MainWindow.Macros.cs` — Macro recording, playback, and management integration.
- `MainWindow.ProfileIcons.cs` — Profile icon rendering helpers.
- `MainWindow.QuickInput.cs` — Quick Input editor integration and draft isolation.
- `MainWindow.Search.cs` — Terminal text search bar, zoom, and fullscreen toggles.
- `MainWindow.SharedClipboardLifetime.cs` — Deferred temporary clipboard file cleanup across windows.
- `MainWindow.Status.cs` — Elevation badge and active working directory status tracking.
- `MainWindow.TabDetach.cs` — Pane detachment and cross-window tab transfer helpers.
- `MainWindow.TabOverflow.cs` — Legacy tab-strip scroll layout logic.
- `MainWindow.TabRename.cs` — Inline tab rename helpers.
- `CloseTabsWindow.xaml` / `CloseTabsWindow.xaml.cs` — Multi-tab close confirmation dialog.
- `DirectoryHistoryWindow.xaml` / `DirectoryHistoryWindow.xaml.cs` — Searchable recent directories dialog.
- `DirectoryReplaceWindow.cs` — Confirmation dialog for replacing an active session from history.
- `FontSettingsWindow.xaml` / `FontSettingsWindow.xaml.cs` / `FontSettingsWindow.*.cs` — Settings dialog (fonts, themes, bell, history, downloads, macros).
- `GuideWindow.xaml` / `GuideWindow.xaml.cs` — Built-in user guide and keyboard shortcut reference window.
- `MacroEditorWindow.cs` / `MacroInteractionWindow.cs` — Macro step editor, password unlock, and interactive step prompts.
- `ProxyManagerWindow.cs` / `ProxyManager.Status.cs` / `ProxyManagerStyles.xaml` / `ProxyOperationWindow.cs` / `ProxyHelpWindow.cs` / `ProxyInfoWindow.cs` / `ProxyLogWindow.cs` / `ProxyUiLanguage.cs` — Proxy Manager UI and diagnostic windows.
- `QuickInputWindow.cs` / `QuickInputWindow.Fluent.cs` — Quick Input multiline & attachment editor dialog.
- `UpdateAvailableWindow.xaml` / `UpdateAvailableWindow.xaml.cs` — Update notification dialog.

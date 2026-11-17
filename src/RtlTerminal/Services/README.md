# Services

Application services for settings, fonts, shell discovery, history, macros, updates, and startup timing.

## Files

- `AppSettings.cs` — Persistent user preferences in the Windows Registry.
- `BundledFonts.cs` — Built-in monospace terminal fonts and font resolution.
- `CommandHistoryStore.cs` — Persistent recent command history store and prefix/substring suggestion ranker.
- `ContextMenuIntegration.cs` — Windows Explorer folder context menu ("Open in RtlTerminal") registration.
- `DirectoryHistoryStore.cs` / `DirectoryOpenMode.cs` — Recent working directory history storage and replacement policy.
- `EnvironmentPaths.cs` / `ProcessDirectory.cs` / `ProcessPrivileges.cs` — Process environment, working directory inspection, and privilege checks.
- `HttpRangeStream.cs` / `OptionalFonts.cs` / `UpdateService.cs` — Optional font downloads and GitHub release update checks.
- `MacroKeys.cs` / `MacroPrivacy.cs` / `MacroSettings.cs` / `MacroStore.cs` / `TerminalMacro.cs` — Terminal macro recording, playback, encryption, and redaction.
- `PaneShortcuts.cs` / `ShortcutCatalog.cs` — Configurable pane shortcuts and shortcut guide catalog.
- `ProfileIcons.cs` / `ShellPath.cs` / `ShellProfiles.cs` / `WslPath.cs` / `WslPathResolver.cs` / `WslProfiles.cs` — Shell profile discovery, path quoting, and WSL integration.
- `ProxyHelpContent.cs` — Localized help text for Proxy Manager.
- `QuickInputDraft.cs` / `QuickInputText.cs` — Quick Input draft state and safe text/attachment preparation.
- `StartupDirectories.cs` / `StartupPerformance.cs` — Startup directory resolution and optional JSONL performance diagnostics.
- `TerminalBell.cs` / `TerminalDirectory.cs` / `TerminalHtmlExporter.cs` / `TerminalLinks.cs` — Bell notifications, OSC directory parsing, HTML export, and link detection.
- `TuiCompatibility.cs` — Per-process TUI compatibility profile detection.
- `UiText.cs` / `UiText.Proxy.cs` / `UiText.Translations.cs` — Multilingual UI string lookup (`English`, `Persian`, `Arabic`).
- `WindowCaptionTheme.cs` / `WindowTransition.cs` / `WindowWorkArea.cs` — Window theme, fullscreen transition, and monitor work-area helpers.

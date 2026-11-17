# RtlTerminal.RenderTests

WPF rendering, ConPTY integration, UI chrome, proxy, macro, Quick Input, and command regression test runner.

## Key Files

- `Program.cs` — Test runner entry point dispatching full suite and targeted diagnostic flags.
- `CommandHelpTests.cs` — Verifies minimal chrome (collapsed top tabs and menu bar) and `help` / feature command interception.
- `TerminalSearchAndZoomTests.cs` — Verifies search bar navigation, font zoom/reset, and fullscreen toggle.
- `WindowCloseTests.cs` — Verifies multi-tab close confirmation and localized exit flows.
- `QuickInputTests.cs` / `QuickInputFluentTests.cs` / `QuickInputCliProbe.cs` — Quick Input UI, draft isolation, and CLI probe tests.
- `Proxy*Tests.cs` — Proxy status, operations, history encryption, and localization tests.
- `Macro*Tests.cs` — Macro recording, playback, privacy, and UI tests.
- `CliLabTests.cs` — CLI Test Lab tests (active when `CLI_TEST_LAB` is defined).
- Remaining `*Tests.cs` / `*Probe.cs` — Targeted tests for RTL shaping, word wrapping, panes, themes, fonts, WSL, and shell profiles.

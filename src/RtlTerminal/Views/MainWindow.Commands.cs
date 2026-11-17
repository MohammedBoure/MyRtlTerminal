using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace RtlTerminal;

public partial class MainWindow
{
    private void UpdateMinimalTitle()
    {
        if (MinimalTitleText is null) return;
        if (_activeTab is null)
        {
            MinimalTitleText.Text = "Rtl Terminal";
            return;
        }

        if (_tabs.Count > 1)
        {
            var index = _tabs.IndexOf(_activeTab) + 1;
            MinimalTitleText.Text = $"Rtl Terminal · {_activeTab.Title} ({index}/{_tabs.Count})";
        }
        else
        {
            MinimalTitleText.Text = $"Rtl Terminal · {_activeTab.Title}";
        }
    }

    private void TrackTypedText(TerminalPane? pane, string text)
    {
        if (pane is null || string.IsNullOrEmpty(text)) return;
        if (IsAlternateScreenActive(pane))
        {
            ResetCommandLine(pane);
            return;
        }
        if (pane.CommandLineDirty) return;

        foreach (var ch in text)
        {
            if (!char.IsControl(ch))
                pane.PendingCommandLine.Append(ch);
        }
    }

    private void TrackPastedText(TerminalPane? pane, string text)
    {
        if (pane is null || string.IsNullOrEmpty(text)) return;
        if (IsAlternateScreenActive(pane) || text.Contains('\r') || text.Contains('\n'))
        {
            pane.PendingCommandLine.Clear();
            pane.CommandLineDirty = true;
            return;
        }
        if (!pane.CommandLineDirty)
            pane.PendingCommandLine.Append(text);
    }

    private void TrackCommandKeyDown(TerminalPane? pane, Key key, ModifierKeys modifiers)
    {
        if (pane is null) return;
        if (IsAlternateScreenActive(pane))
        {
            ResetCommandLine(pane);
            return;
        }

        var control = (modifiers & ModifierKeys.Control) != 0;
        var alt = (modifiers & ModifierKeys.Alt) != 0;

        if (key == Key.Back && !control && !alt)
        {
            if (!pane.CommandLineDirty && pane.PendingCommandLine.Length > 0)
            {
                var removeCount = pane.PendingCommandLine.Length >= 2 &&
                    char.IsLowSurrogate(pane.PendingCommandLine[^1])
                    ? 2 : 1;
                pane.PendingCommandLine.Length -= removeCount;
            }
            return;
        }

        if (key == Key.Escape || (control && key is Key.C or Key.U))
        {
            ResetCommandLine(pane);
            return;
        }

        if (key is Key.Tab or Key.Up or Key.Down or Key.Left or Key.Right or Key.Home or Key.End or Key.Delete ||
            (control && key is Key.W or Key.L))
        {
            pane.PendingCommandLine.Clear();
            pane.CommandLineDirty = true;
        }
    }

    private static void ResetCommandLine(TerminalPane pane)
    {
        pane.PendingCommandLine.Clear();
        pane.CommandLineDirty = false;
    }

    private static bool IsAlternateScreenActive(TerminalPane pane)
    {
        var snapshot = pane.Buffer?.CaptureSnapshot() ?? pane.LastRenderedSnapshot;
        return snapshot?.Modes.AlternateScreen == true;
    }

    private bool TryHandleEnterCommand(TerminalPane? pane)
    {
        if (pane is null) return false;
        var raw = pane.CommandLineDirty ? "" : pane.PendingCommandLine.ToString();
        ResetCommandLine(pane);

        if (string.IsNullOrWhiteSpace(raw) || IsAlternateScreenActive(pane))
            return false;
        if (_macroRecorder is not null || _macroCancellation is not null || _macroPreparing)
            return false;

        return TryExecuteTerminalCommand(pane, raw);
    }

    private bool TryExecuteTerminalCommand(TerminalPane pane, string rawInput)
    {
        var tokens = TokenizeCommand(rawInput.Trim());
        if (tokens.Count == 0) return false;

        var explicitPrefix = false;
        var command = tokens[0].ToLowerInvariant();
        var args = tokens.Skip(1).ToList();

        if (command == "rtl")
        {
            explicitPrefix = true;
            if (args.Count == 0)
            {
                command = "help";
            }
            else
            {
                command = args[0].ToLowerInvariant();
                args = args.Skip(1).ToList();
            }
        }

        // Preserve shell-native commands when passed arguments that belong to the shell.
        if (!explicitPrefix && command == "explorer" && args.Count > 0) return false;
        if (!explicitPrefix && command == "split" && args.Count > 0) return false;
        if (!explicitPrefix && command is "help" or "?" && args.Count > 0 && !IsKnownCommand(args[0]))
            return false;

        if (!IsSupportedInvocation(command, explicitPrefix))
            return false;

        ClearShellCommandLine(pane, rawInput);
        DispatchTerminalCommand(pane, rawInput.Trim(), command, args);
        return true;
    }

    private static bool IsSupportedInvocation(string command, bool explicitPrefix) =>
        IsKnownCommand(command) ||
        (explicitPrefix && command is "find" or "export" or "history" or "exit");

    private static bool IsKnownCommand(string name) =>
        name.ToLowerInvariant() is
            "help" or "?" or
            "opendir" or "explorer" or "dirhistory" or "dirs" or "exportsession" or "export-session" or "preview" or "htmlpreview" or "quit" or
            "search" or "find-text" or "settings" or "prefs" or
            "smartrtl" or "rowrtl" or "compat" or "theme" or "zoom" or "resetzoom" or "fullscreen" or
            "minimize" or "maximize" or "restore" or
            "splitpane" or "split" or "closepane" or "movepane" or
            "quickinput" or "qi" or "proxy" or "macro" or "contextmenu" or
            "lang" or "guide" or "shortcuts" or "updates" or "about" or "status" or
            "profiles" or "newtab" or "tabs" or "tab" or "nexttab" or "prevtab" or "nextpane" or "prevpane"
#if CLI_TEST_LAB
            or "clilab"
#endif
            ;

    private static void ClearShellCommandLine(TerminalPane pane, string rawInput)
    {
        if (pane.Session is not { } session || pane.SessionEnded) return;
        var count = new StringInfo(rawInput).LengthInTextElements;
        if (count > 0)
            session.Write(new string('\x7f', count));
    }

    private void DispatchTerminalCommand(TerminalPane pane, string echoedCommand, string command, List<string> args)
    {
        switch (command)
        {
            case "help" or "?":
                WriteCommandOutput(pane, echoedCommand, BuildHelpOutput(args.FirstOrDefault()));
                return;

            case "opendir" or "explorer":
                _ = OpenPaneDirectoryAsync(pane);
                return;

            case "dirhistory" or "dirs" or "history":
                ShowDirectoryHistory();
                return;

            case "exportsession" or "export-session" or "export":
                ExportSessionMenuItem_Click(this, new RoutedEventArgs());
                return;

            case "preview" or "htmlpreview":
                HtmlPreviewButton_Click(this, new RoutedEventArgs());
                return;

            case "quit" or "exit":
                Close();
                return;

            case "search" or "find-text" or "find":
                OpenSearchBar();
                return;

            case "settings" or "prefs":
                ShowSettings(openThemeTab: false);
                return;

            case "theme":
                HandleThemeCommand(pane, echoedCommand, args);
                return;

            case "smartrtl":
                HandleToggleCommand(pane, echoedCommand, args, "Smart RTL",
                    () => SmartRtlMenuItem.IsChecked,
                    value => SmartRtlMenuItem.IsChecked = value);
                return;

            case "rowrtl":
                HandleToggleCommand(pane, echoedCommand, args, "Row RTL",
                    () => RowRtlMenuItem.IsChecked,
                    value => RowRtlMenuItem.IsChecked = value);
                return;

            case "compat":
                HandleCompatCommand(pane, echoedCommand, args);
                return;

            case "zoom":
                HandleZoomCommand(pane, echoedCommand, args);
                return;

            case "resetzoom":
                ResetFontSize();
                return;

            case "fullscreen":
                ToggleFullScreen();
                return;

            case "minimize":
                WindowState = WindowState.Minimized;
                return;

            case "maximize":
                if (IsFullScreen) ToggleFullScreen();
                WindowState = WindowState.Maximized;
                return;

            case "restore":
                if (IsFullScreen) ToggleFullScreen();
                WindowState = WindowState.Normal;
                return;

            case "splitpane" or "split":
                SplitActivePane();
                return;

            case "closepane":
                ClosePane(pane);
                return;

            case "movepane":
                MovePaneToNewTab(pane);
                return;

            case "quickinput" or "qi":
                OpenQuickInput();
                return;

            case "proxy":
                new ProxyManagerWindow { Owner = this }.ShowDialog();
                return;

            case "macro":
                HandleMacroCommand(pane, echoedCommand, args);
                return;

            case "contextmenu":
                HandleContextMenuCommand(pane, echoedCommand, args);
                return;

            case "lang":
                HandleLanguageCommand(pane, echoedCommand, args);
                return;

            case "guide":
                GuideMenuItem_Click(this, new RoutedEventArgs());
                return;

            case "shortcuts":
                KeyboardShortcutsMenuItem_Click(this, new RoutedEventArgs());
                return;

            case "updates":
                _ = CheckForUpdatesAsync(manual: true);
                return;

            case "about":
                WriteCommandOutput(pane, echoedCommand, BuildAboutOutput());
                return;

            case "status":
                WriteCommandOutput(pane, echoedCommand, BuildStatusOutput(pane));
                return;

            case "profiles":
                WriteCommandOutput(pane, echoedCommand, BuildProfilesOutput());
                return;

            case "newtab":
                HandleNewTabCommand(pane, echoedCommand, args);
                return;

            case "tabs":
                WriteCommandOutput(pane, echoedCommand, BuildTabsOutput());
                return;

            case "tab":
                HandleSwitchTabCommand(pane, echoedCommand, args);
                return;

            case "nexttab":
                SelectRelativeTab(1);
                return;

            case "prevtab":
                SelectRelativeTab(-1);
                return;

            case "nextpane":
                SelectRelativePane(1);
                return;

            case "prevpane":
                SelectRelativePane(-1);
                return;

#if CLI_TEST_LAB
            case "clilab":
                new RtlTerminal.CliLab.CliTestLabWindow(OpenCliLabAsync) { Owner = this }.ShowDialog();
                return;
#endif
        }
    }

    private void HandleThemeCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0)
        {
            ShowSettings(openThemeTab: true);
            return;
        }
        var arg = args[0].ToLowerInvariant();
        if (arg == "dark")
        {
            ApplyColors(AppThemeKind.Dark, null);
            WriteCommandOutput(pane, echoed, "\x1b[32mTheme switched to Dark.\x1b[0m");
            return;
        }
        if (arg == "light")
        {
            ApplyColors(AppThemeKind.Light, null);
            WriteCommandOutput(pane, echoed, "\x1b[32mTheme switched to Light.\x1b[0m");
            return;
        }
        var scheme = ColorScheme.All.FirstOrDefault(s => s.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase));
        if (scheme is not null)
        {
            ApplyColors(scheme.Theme, scheme);
            WriteCommandOutput(pane, echoed, $"\x1b[32mTheme switched to {scheme.Name}.\x1b[0m");
            return;
        }
        WriteCommandOutput(pane, echoed, "\x1b[33mUsage: theme [dark|light]\x1b[0m");
    }

    private void HandleToggleCommand(
        TerminalPane pane, string echoed, List<string> args,
        string label, Func<bool> getter, Action<bool> setter)
    {
        bool next;
        if (args.Count == 0 || args[0].Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            next = !getter();
        }
        else if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase) || args[0] == "1")
        {
            next = true;
        }
        else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase) || args[0] == "0")
        {
            next = false;
        }
        else
        {
            WriteCommandOutput(pane, echoed, $"\x1b[33mUsage: {label.Replace(" ", "").ToLowerInvariant()} [on|off|toggle]\x1b[0m");
            return;
        }

        setter(next);
        WriteCommandOutput(pane, echoed, $"\x1b[32m{label}: {(next ? "ON" : "OFF")}\x1b[0m");
    }

    private void HandleCompatCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0)
        {
            var current = pane.CompatibilityOverride?.ToString() ?? "Auto";
            WriteCommandOutput(pane, echoed, $"Compatibility profile: \x1b[36m{current}\x1b[0m\r\nUsage: compat [auto|default|claude|opentui]");
            return;
        }

        TuiCompatibilityProfile? selected;
        switch (args[0].ToLowerInvariant())
        {
            case "auto": selected = null; break;
            case "default": selected = TuiCompatibilityProfile.Default; break;
            case "claude": selected = TuiCompatibilityProfile.Claude; break;
            case "opentui" or "opencode" or "mimo": selected = TuiCompatibilityProfile.OpenTui; break;
            default:
                WriteCommandOutput(pane, echoed, "\x1b[33mUnknown profile. Use: compat [auto|default|claude|opentui]\x1b[0m");
                return;
        }

        pane.CompatibilityOverride = selected;
        RenderVisiblePanes();
        WriteCommandOutput(pane, echoed, $"\x1b[32mCompatibility profile set to {(selected?.ToString() ?? "Auto")}.\x1b[0m");
    }

    private void HandleZoomCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0)
        {
            WriteCommandOutput(pane, echoed, $"Font size: \x1b[36m{TerminalTextBox.FontSize:0.#} pt\x1b[0m\r\nUsage: zoom [in|out|reset]");
            return;
        }
        switch (args[0].ToLowerInvariant())
        {
            case "in" or "+": ZoomFontSize(1); break;
            case "out" or "-": ZoomFontSize(-1); break;
            case "reset" or "0": ResetFontSize(); break;
            default:
                WriteCommandOutput(pane, echoed, "\x1b[33mUsage: zoom [in|out|reset]\x1b[0m");
                break;
        }
    }

    private async void HandleMacroCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0)
        {
            WriteCommandOutput(pane, echoed, "\x1b[33mUsage: macro [record|stop|list|run <name|#n>|manage|settings]\x1b[0m");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "record":
                MacroRecord_Click(this, new RoutedEventArgs());
                return;
            case "stop":
                MacroStop_Click(this, new RoutedEventArgs());
                return;
            case "manage":
                MacroManage_Click(this, new RoutedEventArgs());
                return;
            case "settings":
                ShowSettings(false, true);
                return;
            case "list":
                try
                {
                    var entries = _macroStore.List().OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();
                    if (entries.Count == 0)
                    {
                        WriteCommandOutput(pane, echoed, "No recorded macros.");
                        return;
                    }
                    var sb = new StringBuilder();
                    sb.AppendLine("\x1b[1;36mRecorded Macros\x1b[0m");
                    for (var i = 0; i < entries.Count; i++)
                        sb.AppendLine($"  \x1b[32m#{i + 1}\x1b[0m  {entries[i]}");
                    sb.Append("\x1b[90mRun with: macro run <name or #index>\x1b[0m");
                    WriteCommandOutput(pane, echoed, sb.ToString());
                }
                catch (Exception ex)
                {
                    WriteCommandOutput(pane, echoed, $"\x1b[31mUnable to read macros: {ex.Message}\x1b[0m");
                }
                return;
            case "run":
                if (args.Count < 2)
                {
                    WriteCommandOutput(pane, echoed, "\x1b[33mUsage: macro run <name|#index>\x1b[0m");
                    return;
                }
                var target = string.Join(" ", args.Skip(1));
                var all = _macroStore.List().OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();
                MacroEntry? match = null;
                if (target.StartsWith('#') && int.TryParse(target[1..], out var num) && num >= 1 && num <= all.Count)
                    match = all[num - 1];
                else if (int.TryParse(target, out var idx) && idx >= 1 && idx <= all.Count)
                    match = all[idx - 1];
                else
                    match = all.FirstOrDefault(e => string.Equals(e.Name, target, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    WriteCommandOutput(pane, echoed, $"\x1b[33mMacro '{target}' not found. Run 'macro list' to see macros.\x1b[0m");
                    return;
                }
                await RunMacro(match);
                return;
            default:
                WriteCommandOutput(pane, echoed, "\x1b[33mUsage: macro [record|stop|list|run <name|#n>|manage|settings]\x1b[0m");
                return;
        }
    }

    private void HandleContextMenuCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0 || args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var installed = ContextMenuIntegration.IsInstalled();
            WriteCommandOutput(pane, echoed, $"Explorer context menu ('Open in RtlTerminal'): \x1b[36m{(installed ? "Enabled" : "Disabled")}\x1b[0m\r\nUsage: contextmenu [on|off]");
            return;
        }

        try
        {
            if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
                ContextMenuIntegration.Install();
            else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                ContextMenuIntegration.Uninstall();
            else
            {
                WriteCommandOutput(pane, echoed, "\x1b[33mUsage: contextmenu [on|off|status]\x1b[0m");
                return;
            }
            RefreshContextMenuIntegrationState();
            WriteCommandOutput(pane, echoed, $"\x1b[32mExplorer context menu {(ContextMenuIntegration.IsInstalled() ? "enabled" : "disabled")}.\x1b[0m");
        }
        catch (Exception ex)
        {
            WriteCommandOutput(pane, echoed, $"\x1b[31mCould not change folder context menu: {ex.Message}\x1b[0m");
        }
    }

    private void HandleLanguageCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0)
        {
            WriteCommandOutput(pane, echoed, $"UI language: \x1b[36m{UiText.Current}\x1b[0m\r\nUsage: lang [en|fa|ar]");
            return;
        }

        UiLanguage? target = args[0].ToLowerInvariant() switch
        {
            "en" or "english" => UiLanguage.English,
            "fa" or "persian" or "farsi" => UiLanguage.Persian,
            "ar" or "arabic" => UiLanguage.Arabic,
            _ => null
        };

        if (target is not { } language)
        {
            WriteCommandOutput(pane, echoed, "\x1b[33mUnknown language. Use: lang [en|fa|ar]\x1b[0m");
            return;
        }

        UiText.Select(language);
        WriteCommandOutput(pane, echoed, $"\x1b[32mInterface language set to {language}.\x1b[0m");
    }

    private void HandleNewTabCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0)
        {
            CreateTerminalTab(_defaultProfile);
            return;
        }

        var query = string.Join(" ", args).Trim();
        var available = GetAvailableProfiles();
        ShellMenuItem? selected = null;

        if (int.TryParse(query, out var idx) && idx >= 1 && idx <= available.Count)
            selected = available[idx - 1];
        else
        {
            selected = query.ToLowerInvariant() switch
            {
                "cmd" or "command" or "commandprompt" => available.FirstOrDefault(p => p.Profile == TerminalProfile.CommandPrompt),
                "powershell" or "ps" => available.FirstOrDefault(p => p.Profile == TerminalProfile.PowerShell),
                "pwsh" or "ps7" or "powershell7" => available.FirstOrDefault(p => p.Profile == TerminalProfile.PowerShell7),
                "bash" or "gitbash" or "git" => available.FirstOrDefault(p => p.Profile == TerminalProfile.GitBash),
                "wsl" => available.FirstOrDefault(p => p.Profile == TerminalProfile.Wsl),
                _ => available.FirstOrDefault(p =>
                    p.Header.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (p.WslDistribution?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            };
        }

        if (selected is not { Header: not null } found)
        {
            WriteCommandOutput(pane, echoed, $"\x1b[33mProfile '{query}' not found. Run 'profiles' to list available shells.\x1b[0m");
            return;
        }

        CreateTerminalTab(found.Profile, wslDistribution: found.WslDistribution);
    }

    private void HandleSwitchTabCommand(TerminalPane pane, string echoed, List<string> args)
    {
        if (args.Count == 0 || !int.TryParse(args[0], out var index) || index < 1 || index > _tabs.Count)
        {
            WriteCommandOutput(pane, echoed, $"\x1b[33mUsage: tab <1..{_tabs.Count}>\x1b[0m");
            return;
        }
        SelectTab(_tabs[index - 1]);
    }

    private void WriteCommandOutput(TerminalPane pane, string echoedCommand, string output)
    {
        // Let ConPTY echo the backspaces that cleared the command from the shell buffer,
        // then render our output and send an empty Enter to print a fresh shell prompt.
        _ = Task.Run(async () =>
        {
            await Task.Delay(80);
            if (_windowClosed || pane.Session is not { } session || pane.SessionEnded || pane.Buffer is not { } buffer)
                return;

            var snapshot = buffer.Process($"{echoedCommand}\r\n{output}\r\n");
            pane.FollowOutput = true;
            QueueRender(pane, snapshot);
            session.Write("\r");
        });
    }

    private string BuildHelpOutput(string? topic)
    {
        if (!string.IsNullOrWhiteSpace(topic) && !topic.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = topic.ToLowerInvariant();
            if (TryBuildCommandDetail(normalized, out var detail))
                return detail;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"\x1b[1;36mRtlTerminal v{AppVersion.Display} — Command Reference\x1b[0m");
        sb.AppendLine("\x1b[90m(All menu bar and tab bar features are accessible here or via 'rtl <cmd>')\x1b[0m");
        sb.AppendLine();
        sb.AppendLine("\x1b[1;33mFile & Session\x1b[0m");
        sb.AppendLine("  \x1b[32mopendir\x1b[0m                    Open current folder in Explorer");
        sb.AppendLine("  \x1b[32mdirhistory\x1b[0m                 Open recent directories history");
        sb.AppendLine("  \x1b[32mexportsession\x1b[0m              Export active session to UTF-8 text");
        sb.AppendLine("  \x1b[32mpreview\x1b[0m                    Open HTML preview in browser");
        sb.AppendLine("  \x1b[32mquit\x1b[0m                       Close RtlTerminal window");
        sb.AppendLine();
        sb.AppendLine("\x1b[1;33mEdit & Settings\x1b[0m");
        sb.AppendLine("  \x1b[32msearch\x1b[0m                     Open search bar (Ctrl+F)");
        sb.AppendLine("  \x1b[32msettings\x1b[0m                   Open Settings (fonts, bell, history, macros)");
        sb.AppendLine();
        sb.AppendLine("\x1b[1;33mView & RTL\x1b[0m");
        sb.AppendLine("  \x1b[32msmartrtl\x1b[0m [on|off]          Toggle Smart RTL line layout");
        sb.AppendLine("  \x1b[32mrowrtl\x1b[0m [on|off]            Toggle Row RTL for all lines");
        sb.AppendLine("  \x1b[32mcompat\x1b[0m [auto|default|claude|opentui]  TUI compatibility profile");
        sb.AppendLine("  \x1b[32mtheme\x1b[0m [dark|light]         Open theme settings or switch theme");
        sb.AppendLine("  \x1b[32mzoom\x1b[0m [in|out|reset]        Adjust font size (Ctrl++ / Ctrl+- / Ctrl+0)");
        sb.AppendLine("  \x1b[32mfullscreen\x1b[0m                 Toggle fullscreen (F11)");
        sb.AppendLine("  \x1b[32mminimize\x1b[0m / \x1b[32mmaximize\x1b[0m / \x1b[32mrestore\x1b[0m  Window state (Alt+Drag moves window)");
        sb.AppendLine("  \x1b[32msplitpane\x1b[0m                  Split active pane (Ctrl+Shift+D)");
        sb.AppendLine("  \x1b[32mclosepane\x1b[0m                  Close active pane (Ctrl+Shift+W)");
        sb.AppendLine("  \x1b[32mmovepane\x1b[0m                   Move active pane to a separate tab");
        sb.AppendLine();
        sb.AppendLine("\x1b[1;33mTools\x1b[0m");
        sb.AppendLine("  \x1b[32mquickinput\x1b[0m                 Open Quick Input editor (Ctrl+Shift+Space)");
        sb.AppendLine("  \x1b[32mproxy\x1b[0m                      Open Proxy Manager");
        sb.AppendLine("  \x1b[32mmacro\x1b[0m <record|stop|list|run|manage|settings>  Manage/run macros");
        sb.AppendLine("  \x1b[32mcontextmenu\x1b[0m [on|off|status] Toggle 'Open in RtlTerminal' folder menu");
#if CLI_TEST_LAB
        sb.AppendLine("  \x1b[32mclilab\x1b[0m                     Open CLI Test Lab (preview builds)");
#endif
        sb.AppendLine();
        sb.AppendLine("\x1b[1;33mShells & Panes\x1b[0m");
        sb.AppendLine("  \x1b[32mprofiles\x1b[0m                   List available shells (PowerShell, CMD, WSL, Git Bash)");
        sb.AppendLine("  \x1b[32mnewtab\x1b[0m [profile]           Open a new session tab (Ctrl+Shift+T)");
        sb.AppendLine("  \x1b[32mtabs\x1b[0m / \x1b[32mtab\x1b[0m <n>             List or switch sessions (Ctrl+Tab)");
        sb.AppendLine("  \x1b[32mnextpane\x1b[0m / \x1b[32mprevpane\x1b[0m        Switch between split panes (Ctrl+Shift+Right/Left)");
        sb.AppendLine();
        sb.AppendLine("\x1b[1;33mHelp & Info\x1b[0m");
        sb.AppendLine("  \x1b[32mstatus\x1b[0m                     Show active directory, profile, RTL & theme info");
        sb.AppendLine("  \x1b[32mlang\x1b[0m [en|fa|ar]            Switch interface language (English / فارسی / العربية)");
        sb.AppendLine("  \x1b[32mguide\x1b[0m                      Open RtlTerminal Guide");
        sb.AppendLine("  \x1b[32mshortcuts\x1b[0m                  Open Keyboard Shortcuts guide");
        sb.AppendLine("  \x1b[32mupdates\x1b[0m                    Check for updates");
        sb.AppendLine("  \x1b[32mabout\x1b[0m                      Show About info");
        sb.AppendLine();
        sb.Append("\x1b[90mTip: Run 'help <command>' for details, or 'Get-Help' / 'cmd /c help' for shell help.\x1b[0m");
        return sb.ToString();
    }

    private static bool TryBuildCommandDetail(string command, out string output)
    {
        output = command switch
        {
            "opendir" or "explorer" => "\x1b[32mopendir\x1b[0m — Opens the active terminal's current folder in Windows Explorer.\r\nAlias: explorer (when called with no arguments).",
            "dirhistory" or "dirs" => "\x1b[32mdirhistory\x1b[0m — Opens the searchable recent directories history window.",
            "exportsession" or "export-session" => "\x1b[32mexportsession\x1b[0m — Saves the current terminal buffer as a UTF-8 .txt file.",
            "preview" or "htmlpreview" => "\x1b[32mpreview\x1b[0m — Generates a local HTML preview of the terminal output and opens it in your browser.",
            "search" or "find-text" => "\x1b[32msearch\x1b[0m — Opens the floating search bar (Shortcut: Ctrl+F, or use 'rtl find').",
            "settings" or "prefs" => "\x1b[32msettings\x1b[0m — Opens Settings (Fonts, Theme, Bell, Directory History, Macros, Shortcuts).",
            "smartrtl" => "\x1b[32msmartrtl\x1b[0m [on|off|toggle] — Applies right-to-left layout only to lines containing RTL letters.",
            "rowrtl" => "\x1b[32mrowrtl\x1b[0m [on|off|toggle] — Forces right-to-left direction on every row.",
            "compat" => "\x1b[32mcompat\x1b[0m [auto|default|claude|opentui] — Sets the TUI compatibility profile for the active pane.",
            "theme" => "\x1b[32mtheme\x1b[0m [dark|light] — Switches between Dark and Light themes, or opens the Theme tab in Settings.",
            "zoom" or "resetzoom" => "\x1b[32mzoom\x1b[0m [in|out|reset] — Changes terminal font size (Shortcuts: Ctrl++, Ctrl+-, Ctrl+0).",
            "fullscreen" => "\x1b[32mfullscreen\x1b[0m — Toggles fullscreen mode (Shortcut: F11).",
            "splitpane" or "split" => "\x1b[32msplitpane\x1b[0m — Splits the active pane side-by-side or stacked (Shortcut: Ctrl+Shift+D).",
            "closepane" => "\x1b[32mclosepane\x1b[0m — Closes the active pane (Shortcut: Ctrl+Shift+W).",
            "movepane" => "\x1b[32mmovepane\x1b[0m — Detaches the active split pane into its own background tab.",
            "quickinput" or "qi" => "\x1b[32mquickinput\x1b[0m (alias: qi) — Opens the Quick Input multiline/attachment editor (Shortcut: Ctrl+Shift+Space).",
            "proxy" => "\x1b[32mproxy\x1b[0m — Opens Proxy Manager for Environment, System, WinHTTP, and developer tools.",
            "macro" => "\x1b[32mmacro\x1b[0m <record|stop|list|run <name|#n>|manage|settings> — Record, play, or manage encrypted terminal macros.",
            "contextmenu" => "\x1b[32mcontextmenu\x1b[0m [on|off|status] — Adds or removes 'Open in RtlTerminal' from the Windows folder right-click menu.",
            "lang" => "\x1b[32mlang\x1b[0m [en|fa|ar] — Switches the UI dialog language (English, Persian, Arabic).",
            "newtab" or "profiles" or "tabs" or "tab" => "\x1b[32mprofiles\x1b[0m / \x1b[32mnewtab\x1b[0m [profile] / \x1b[32mtabs\x1b[0m / \x1b[32mtab\x1b[0m <n> — Manage hidden shell sessions and switch between them.",
#if CLI_TEST_LAB
            "clilab" => "\x1b[32mclilab\x1b[0m — Opens CLI Test Lab (development/preview builds only).",
#endif
            _ => ""
        };
        return output.Length > 0;
    }

    private string BuildProfilesOutput()
    {
        var profiles = GetAvailableProfiles();
        var sb = new StringBuilder();
        sb.AppendLine("\x1b[1;36mAvailable Shell Profiles\x1b[0m");
        for (var i = 0; i < profiles.Count; i++)
        {
            var p = profiles[i];
            var alias = p.Profile switch
            {
                TerminalProfile.PowerShell7 => "pwsh",
                TerminalProfile.PowerShell => "powershell",
                TerminalProfile.CommandPrompt => "cmd",
                TerminalProfile.GitBash => "bash",
                TerminalProfile.Wsl => p.WslDistribution ?? "wsl",
                _ => "cmd"
            };
            sb.AppendLine($"  \x1b[32m{i + 1}\x1b[0m  {p.Header,-22} \x1b[90m(newtab {alias})\x1b[0m");
        }
        return sb.ToString().TrimEnd();
    }

    private string BuildTabsOutput()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\x1b[1;36mOpen Sessions\x1b[0m");
        for (var i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var active = ReferenceEquals(tab, _activeTab) ? "\x1b[32m*\x1b[0m" : " ";
            sb.AppendLine($" {active} \x1b[32m{i + 1}\x1b[0m  {tab.Title}  \x1b[90m({tab.Panes.Count} pane(s))\x1b[0m");
        }
        sb.Append("\x1b[90mSwitch with: tab <n>, nexttab, prevtab (Ctrl+Tab)\x1b[0m");
        return sb.ToString();
    }

    private string BuildStatusOutput(TerminalPane pane)
    {
        string? dir;
        lock (pane.DirectoryHistoryGate) dir = pane.CurrentDirectory;
        dir ??= TerminalDirectory.FromReport(pane.Buffer?.CaptureSnapshot().WorkingDirectory, pane.WslDistribution) ?? "(unknown)";
        var compat = pane.CompatibilityOverride?.ToString() ?? "Auto";
        var sb = new StringBuilder();
        sb.AppendLine("\x1b[1;36mRtlTerminal Status\x1b[0m");
        sb.AppendLine($"  Profile       : {pane.Title}");
        sb.AppendLine($"  Directory     : {dir}");
        sb.AppendLine($"  Smart RTL     : {(SmartRtlMenuItem.IsChecked ? "ON" : "OFF")}");
        sb.AppendLine($"  Row RTL       : {(RowRtlMenuItem.IsChecked ? "ON" : "OFF")}");
        sb.AppendLine($"  Compatibility : {compat}");
        sb.AppendLine($"  Theme         : {AppTheme.Current}");
        sb.AppendLine($"  Font          : {BundledFonts.DisplayName(TerminalTextBox.FontFamily)} ({TerminalTextBox.FontSize:0.#} pt)");
        sb.AppendLine($"  UI Language   : {UiText.Current}");
        sb.Append($"  Sessions      : {_tabs.Count} tab(s), {_activeTab?.Panes.Count ?? 0} pane(s) in current");
        return sb.ToString();
    }

    private static string BuildAboutOutput() =>
        $"\x1b[1;36mRtl Terminal v{AppVersion.Display}\x1b[0m\r\n" +
        "by behnamapps\r\n" +
        $"Developer : behnam tajadini\r\n" +
        "Source    : https://github.com/mirbehnam/RtlTerminal";

    private static List<string> TokenizeCommand(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';

        foreach (var ch in input)
        {
            if (inQuotes)
            {
                if (ch == quoteChar)
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch is '"' or '\'')
            {
                inQuotes = true;
                quoteChar = ch;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}

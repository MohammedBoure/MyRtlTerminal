using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RtlTerminal;

public partial class MainWindow
{
    private sealed record SuggestionRow(string Command, bool IsHistory)
    {
        public string KindLabel => IsHistory ? "history" : "command";
    }

    private static readonly string[] BuiltInSuggestionCommands =
    [
        "help", "status", "settings", "theme", "theme dark", "theme light",
        "proxy", "quickinput", "search", "preview", "opendir", "dirhistory",
        "exportsession", "smartrtl on", "smartrtl off", "rowrtl on", "rowrtl off",
        "compat auto", "compat claude", "compat opentui", "compat default",
        "zoom in", "zoom out", "zoom reset", "fullscreen", "minimize", "maximize", "restore",
        "splitpane", "closepane", "movepane", "macro record", "macro stop", "macro list",
        "macro manage", "macro settings", "contextmenu status", "contextmenu on", "contextmenu off",
        "profiles", "newtab", "tabs", "nexttab", "prevtab", "nextpane", "prevpane",
        "lang en", "lang fa", "lang ar", "guide", "shortcuts", "updates", "about",
        "suggestions on", "suggestions off", "quit"
#if CLI_TEST_LAB
        , "clilab"
#endif
    ];

    private CommandHistoryStore _commandHistory = new();
    private bool _commandSuggestionsEnabled = true;

    private void InitializeCommandSuggestions()
    {
        _commandHistory.EnsureLoadedAsync();
    }

    private void HideCommandSuggestions()
    {
        if (CommandSuggestionsPopup is null) return;
        CommandSuggestionsPopup.Visibility = Visibility.Collapsed;
        CommandSuggestionsList.ItemsSource = null;
        CommandSuggestionsList.SelectedIndex = -1;
    }

    private void UpdateCommandSuggestions(TerminalPane? pane)
    {
        if (!_commandSuggestionsEnabled || pane is null || !ReferenceEquals(pane, ActivePane) ||
            IsAlternateScreenActive(pane) || pane.CommandLineDirty ||
            _macroRecorder is not null || _macroCancellation is not null || _macroPreparing)
        {
            HideCommandSuggestions();
            return;
        }

        var prefix = pane.PendingCommandLine.ToString();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            HideCommandSuggestions();
            return;
        }

        var suggestions = _commandHistory.Suggest(prefix, BuiltInSuggestionCommands, maxCount: 8);
        if (suggestions.Count == 0)
        {
            HideCommandSuggestions();
            return;
        }

        CommandSuggestionsList.ItemsSource = suggestions.Select(s => new SuggestionRow(s.Command, s.IsHistory)).ToList();
        CommandSuggestionsList.SelectedIndex = -1;
        PositionCommandSuggestionsPopup();
        CommandSuggestionsPopup.Visibility = Visibility.Visible;
    }

    private void PositionCommandSuggestionsPopup()
    {
        if (TerminalOverlayGrid is null || CommandSuggestionsPopup is null) return;

        var hostWidth = TerminalOverlayGrid.ActualWidth;
        var hostHeight = TerminalOverlayGrid.ActualHeight;
        if (hostWidth <= 0 || hostHeight <= 0) return;

        CommandSuggestionsPopup.Measure(new Size(Math.Min(540, Math.Max(280, hostWidth - 20)), 260));
        var popupWidth = Math.Max(280, CommandSuggestionsPopup.DesiredSize.Width);
        var popupHeight = Math.Max(60, CommandSuggestionsPopup.DesiredSize.Height);

        double left = 12;
        double top = Math.Max(8, hostHeight - popupHeight - 24);

        if (ActiveView.TryGetCursorBounds(TerminalOverlayGrid, out var cursorBottomLeft, out var rowHeight))
        {
            left = cursorBottomLeft.X;
            var spaceBelow = hostHeight - cursorBottomLeft.Y;
            top = spaceBelow >= popupHeight + 8
                ? cursorBottomLeft.Y + 4
                : Math.Max(8, cursorBottomLeft.Y - rowHeight - popupHeight - 4);
        }

        left = Math.Clamp(left, 8, Math.Max(8, hostWidth - popupWidth - 18));
        top = Math.Clamp(top, 8, Math.Max(8, hostHeight - popupHeight - 8));
        CommandSuggestionsPopup.Margin = new Thickness(left, top, 0, 0);
    }

    private bool HandleSuggestionKeyDown(TerminalPane? pane, Key key, ModifierKeys modifiers)
    {
        if (CommandSuggestionsPopup is null ||
            CommandSuggestionsPopup.Visibility != Visibility.Visible ||
            CommandSuggestionsList.Items.Count == 0)
        {
            return false;
        }

        if (modifiers != ModifierKeys.None)
        {
            if (modifiers == ModifierKeys.Control && key is Key.C or Key.U or Key.L or Key.W)
                HideCommandSuggestions();
            return false;
        }

        var count = CommandSuggestionsList.Items.Count;
        switch (key)
        {
            case Key.Down:
            {
                var next = CommandSuggestionsList.SelectedIndex < 0
                    ? 0
                    : (CommandSuggestionsList.SelectedIndex + 1) % count;
                CommandSuggestionsList.SelectedIndex = next;
                CommandSuggestionsList.ScrollIntoView(CommandSuggestionsList.SelectedItem);
                return true;
            }
            case Key.Up:
            {
                var prev = CommandSuggestionsList.SelectedIndex <= 0
                    ? count - 1
                    : CommandSuggestionsList.SelectedIndex - 1;
                CommandSuggestionsList.SelectedIndex = prev;
                CommandSuggestionsList.ScrollIntoView(CommandSuggestionsList.SelectedItem);
                return true;
            }
            case Key.Escape:
                HideCommandSuggestions();
                return true;

            case Key.Tab when CommandSuggestionsList.SelectedIndex >= 0:
                AcceptSelectedSuggestion(pane, execute: false);
                return true;

            case Key.Enter when CommandSuggestionsList.SelectedIndex >= 0:
                AcceptSelectedSuggestion(pane, execute: true);
                return true;

            default:
                return false;
        }
    }

    private void AcceptSelectedSuggestion(TerminalPane? pane, bool execute)
    {
        if (CommandSuggestionsList.SelectedItem is not SuggestionRow row)
        {
            HideCommandSuggestions();
            return;
        }
        AcceptSuggestion(pane, row.Command, execute);
    }

    private void AcceptSuggestion(TerminalPane? pane, string selectedCommand, bool execute)
    {
        HideCommandSuggestions();
        if (pane is null) return;

        var typed = pane.CommandLineDirty ? "" : pane.PendingCommandLine.ToString();
        ResetCommandLine(pane);
        ClearShellCommandLine(pane, typed);

        if (!execute)
        {
            pane.PendingCommandLine.Append(selectedCommand);
            pane.CommandLineDirty = false;
            if (pane.Session is { } session && !pane.SessionEnded)
                session.Write(selectedCommand);
            pane.HasUserInput = true;
            ActiveView.Focus();
            return;
        }

        _commandHistory.Record(selectedCommand);
        if (TryExecuteTerminalCommand(pane, selectedCommand, clearShellLine: false))
        {
            ActiveView.Focus();
            return;
        }

        if (pane.Session is { } execSession && !pane.SessionEnded)
            execSession.Write(selectedCommand + "\r");
        pane.HasUserInput = true;
        ActiveView.Focus();
    }

    private void CommandSuggestionsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not ListBoxItem)
            source = VisualTreeHelper.GetParent(source);

        if (source is ListBoxItem { DataContext: SuggestionRow row })
        {
            e.Handled = true;
            AcceptSuggestion(ActivePane, row.Command, execute: false);
        }
    }
}

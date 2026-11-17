using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckMinimalChromeAndCommands()
    {
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        UiText.Select(UiLanguage.English, persist: false);

        var window = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Call(string name, params object?[] args) =>
            typeof(MainWindow).GetMethod(name, flags | BindingFlags.Static)!.Invoke(window, args);

        try
        {
            var titleBar = (FrameworkElement)window.FindName("TitleBarArea");
            var legacyTabs = (FrameworkElement)window.FindName("LegacyTabsChrome");
            var menuBar = (FrameworkElement)window.FindName("MenuBarArea");
            var adminBadge = (Border)window.FindName("AdministratorBadge");

            if (titleBar.Visibility != Visibility.Collapsed)
                throw new Exception("Top title bar must be collapsed.");
            if (legacyTabs.Visibility != Visibility.Collapsed)
                throw new Exception("Top tabs chrome must be collapsed.");
            if (menuBar.Visibility != Visibility.Collapsed)
                throw new Exception("File/Edit menu bar must be collapsed.");

            Call("SetElevationBadge", false);
            if (adminBadge.Visibility != Visibility.Collapsed)
                throw new Exception("Normal process must hide administrator badge.");
            Call("SetElevationBadge", true);
            if (adminBadge.Visibility != Visibility.Visible)
                throw new Exception("Elevated process must show administrator badge.");

            var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
            var paneType = typeof(MainWindow).GetNestedType("TerminalPane", BindingFlags.NonPublic)!;
            var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
            var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
            var pane = Call("CreatePane", Enum.ToObject(profileType, 1))!;
            var buffer = new TerminalBuffer(100, 30);
            paneType.GetProperty("Buffer")!.SetValue(pane, buffer);
            var tab = Activator.CreateInstance(tabType, pane)!;
            tabs.Add(tab);
            Call("ShowTab", tab);

            // Verify help output covers every bar feature.
            var helpText = (string)Call("BuildHelpOutput", [null])!;
            foreach (var expected in new[]
            {
                "opendir", "dirhistory", "exportsession", "preview", "quit",
                "search", "settings", "smartrtl", "rowrtl", "compat", "theme",
                "zoom", "fullscreen", "minimize", "maximize", "restore", "splitpane", "closepane", "movepane",
                "quickinput", "proxy", "macro", "contextmenu",
                "profiles", "newtab", "tabs", "tab", "status", "lang",
                "guide", "shortcuts", "updates", "about"
            })
            {
                if (!helpText.Contains(expected, StringComparison.Ordinal))
                    throw new Exception($"Help output is missing command '{expected}'.");
            }

            // Verify command tracking & pass-through rules.
            Call("TrackTypedText", pane, "helx");
            Call("TrackCommandKeyDown", pane, Key.Back, ModifierKeys.None);
            Call("TrackTypedText", pane, "p");
            var pending = paneType.GetProperty("PendingCommandLine")!.GetValue(pane)!.ToString();
            if (pending != "help")
                throw new Exception($"Command line tracking failed: expected 'help', got '{pending}'.");

            if (!(bool)Call("TryHandleEnterCommand", pane)!)
                throw new Exception("Typed 'help' command was not intercepted.");

            // Shell commands with arguments must pass through to the shell.
            Call("TrackTypedText", pane, "help Get-Process");
            if ((bool)Call("TryHandleEnterCommand", pane)!)
                throw new Exception("'help Get-Process' should pass through to the shell.");

            Call("TrackTypedText", pane, "explorer .");
            if ((bool)Call("TryHandleEnterCommand", pane)!)
                throw new Exception("'explorer .' should pass through to the shell.");

            Call("TrackTypedText", pane, "split -l 10");
            if ((bool)Call("TryHandleEnterCommand", pane)!)
                throw new Exception("'split -l 10' should pass through to the shell.");

            // Alternate screen must disable command interception.
            buffer.Process("\x1b[?1049h");
            Call("TrackTypedText", pane, "help");
            if ((bool)Call("TryHandleEnterCommand", pane)!)
                throw new Exception("Alternate screen must not intercept 'help'.");
            buffer.Process("\x1b[?1049l");

            // Verify feature commands update state cleanly.
            Call("TrackTypedText", pane, "smartrtl off");
            if (!(bool)Call("TryHandleEnterCommand", pane)! || ((MenuItem)window.FindName("SmartRtlMenuItem")).IsChecked)
                throw new Exception("'smartrtl off' did not disable Smart RTL.");
            Call("TrackTypedText", pane, "smartrtl on");
            if (!(bool)Call("TryHandleEnterCommand", pane)! || !((MenuItem)window.FindName("SmartRtlMenuItem")).IsChecked)
                throw new Exception("'smartrtl on' did not enable Smart RTL.");

            Call("TrackTypedText", pane, "compat claude");
            if (!(bool)Call("TryHandleEnterCommand", pane)! ||
                paneType.GetProperty("CompatibilityOverride")!.GetValue(pane)?.ToString() != "Claude")
                throw new Exception("'compat claude' did not set compatibility profile.");
            Call("TrackTypedText", pane, "compat auto");
            if (!(bool)Call("TryHandleEnterCommand", pane)! ||
                paneType.GetProperty("CompatibilityOverride")!.GetValue(pane) is not null)
                throw new Exception("'compat auto' did not reset compatibility profile.");

            Call("TrackTypedText", pane, "theme light");
            if (!(bool)Call("TryHandleEnterCommand", pane)! || AppTheme.Current != AppThemeKind.Light)
                throw new Exception("'theme light' did not switch to Light theme.");
            Call("TrackTypedText", pane, "theme dark");
            if (!(bool)Call("TryHandleEnterCommand", pane)! || AppTheme.Current != AppThemeKind.Dark)
                throw new Exception("'theme dark' did not switch to Dark theme.");

            Call("TrackTypedText", pane, "lang ar");
            if (!(bool)Call("TryHandleEnterCommand", pane)! || UiText.Current != UiLanguage.Arabic)
                throw new Exception("'lang ar' did not switch UI language to Arabic.");
            Call("TrackTypedText", pane, "lang en");
            if (!(bool)Call("TryHandleEnterCommand", pane)! || UiText.Current != UiLanguage.English)
                throw new Exception("'lang en' did not restore UI language to English.");

            Console.WriteLine("PASS minimal chrome (no top tabs/menu bar) and full help/command dispatcher");
        }
        finally
        {
            var closing = typeof(MainWindow).GetMethod("Window_Closing", flags)!;
            window.Closing -= (System.ComponentModel.CancelEventHandler)closing.CreateDelegate(
                typeof(System.ComponentModel.CancelEventHandler), window);
            window.Close();
        }
    }
}

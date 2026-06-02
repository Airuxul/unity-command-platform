using System;
using System.Collections.Generic;
using Air.UnityConnector.Invoke;
using UnityEditor;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    public class MenuCommand : CliCommand<MenuParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<MenuParams>(
            CommandNames.Menu,
            CommandHostScope.Editor,
            "Execute a Unity menu item by path");

        private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "File/Quit",
        };

        public override void Run(MenuParams p)
        {
            var menuPath = p.MenuPath;
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                const string error = "Parameter 'menu_path' is required (e.g. File/Save Project).";
                CompleteFail(error);
                return;
            }

            if (Blocklist.Contains(menuPath))
            {
                var error = $"Execution of '{menuPath}' is blocked for safety.";
                CompleteFail(error);
                return;
            }

            if (!EditorApplication.ExecuteMenuItem(menuPath))
            {
                var error = $"Failed to execute menu item '{menuPath}'.";
                CompleteFail(error);
                return;
            }

            CompleteSuccess(InvokeResult.Ok($"menu executed: {menuPath}"));
        }
    }
}

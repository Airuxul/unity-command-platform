using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityCliConnector.Builtin
{
    [CliCommand(
        "editor.menu",
        Scope = CommandScope.Editor,
        Description = "Execute a Unity menu item by path",
        Aliases = "menu")]
    public static class MenuCommand
    {
        private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "File/Quit",
        };

        public static CommandResult Run(CliParams p)
        {
            var menuPath = p.GetString("menu_path") ?? p.GetString("path");
            if (string.IsNullOrWhiteSpace(menuPath))
                return CommandResult.Fail("Parameter 'menu_path' is required (e.g. File/Save Project).");

            if (Blocklist.Contains(menuPath))
                return CommandResult.Fail($"Execution of '{menuPath}' is blocked for safety.");

            if (!EditorApplication.ExecuteMenuItem(menuPath))
                return CommandResult.Fail($"Failed to execute menu item '{menuPath}'.");

            return CommandResult.Success(new Dictionary<string, object>
            {
                ["menu_path"] = menuPath,
                ["executed"] = true,
            });
        }
    }
}

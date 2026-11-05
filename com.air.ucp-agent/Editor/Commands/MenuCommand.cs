using System;
using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using UnityEditor;
using Air.UcpAgent.Params;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class MenuCommand : EditorActionCommand<MenuParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<MenuParams>(
            CommandNames.Menu,
            CommandHostScope.Editor,
            "Execute a Unity menu item by path");

        private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "File/Quit",
        };

        protected override Dictionary<string, object> Execute(MenuParams p)
        {
            var menuPath = p.MenuPath;
            if (string.IsNullOrWhiteSpace(menuPath))
                throw new ArgumentException("Parameter 'menu_path' is required (e.g. File/Save Project).");

            if (Blocklist.Contains(menuPath))
                throw new InvalidOperationException($"Execution of '{menuPath}' is blocked for safety.");

            if (!EditorApplication.ExecuteMenuItem(menuPath))
                throw new InvalidOperationException($"Failed to execute menu item '{menuPath}'.");

            return null;
        }

        protected override string Message(MenuParams p) => $"menu executed: {p.MenuPath}";
    }
}

using System.Collections.Generic;
using UnityCliConnector.Editor.Services;

namespace UnityCliConnector.Builtin
{
    [CliCommand(
        "editor.console",
        Scope = CommandScope.Editor,
        Description = "Read or clear Unity Editor console logs",
        Aliases = "console,logs")]
    public static class ConsoleCommand
    {
        public static CommandResult Run(CliParams p)
        {
            if (!UnityConsoleReader.IsReady)
            {
                return CommandResult.Fail(
                    $"Console reader unavailable: {UnityConsoleReader.InitError}");
            }

            if (p.GetBool("clear"))
            {
                UnityConsoleReader.Clear();
                return CommandResult.Success(new Dictionary<string, object>
                {
                    ["cleared"] = true,
                });
            }

            var lines = p.GetInt("lines") ?? p.GetInt("count");
            var typeFilter = p.Has("type")
                ? p.GetString("type")
                : "error,warning";

            var entries = UnityConsoleReader.Read(new UnityConsoleReader.ConsoleReadOptions
            {
                TypeFilter = typeFilter,
                MaxEntries = lines,
                Stacktrace = p.GetString("stacktrace", "user"),
            });

            return CommandResult.Success(new Dictionary<string, object>
            {
                ["count"] = entries.Count,
                ["entries"] = entries,
                ["types"] = typeFilter,
                ["stacktrace"] = p.GetString("stacktrace", "user"),
            });
        }
    }
}

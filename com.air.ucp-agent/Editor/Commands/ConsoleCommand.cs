using Air.UcpAgent.Cli;
using System;
using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class ConsoleCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ConsoleParams>(
            CommandNames.Console,
            CommandHostScope.Editor,
            "Read or clear Unity Editor console logs")
        {
            RunsWhileBusy = true,
        };

        public static object Execute(ConsoleParams p, CommandContext ctx)
        {
            ctx.Message = p.Clear ? "console cleared" : "console entries";
            if (!UnityConsoleReader.IsReady)
                throw new InvalidOperationException($"Console reader unavailable: {UnityConsoleReader.InitError}");

            if (p.Clear)
            {
                UnityConsoleReader.Clear();
                return null;
            }

            var stacktrace = string.IsNullOrEmpty(p.Stacktrace) ? "user" : p.Stacktrace;
            var entries = UnityConsoleReader.Read(new UnityConsoleReader.ConsoleReadOptions
            {
                TypeFilter = p.Type ?? "error,warning",
                MaxEntries = p.Lines,
                Stacktrace = stacktrace,
            }, out var matched);

            return new Dictionary<string, object>
            {
                ["count"] = entries.Count,
                ["matched"] = matched,
                ["truncated"] = matched > entries.Count,
                ["entries"] = entries,
                ["types"] = p.Type ?? "error,warning",
                ["stacktrace"] = stacktrace,
            };
        }
    }
}

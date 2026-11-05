using System;
using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class ConsoleCommand : EditorActionCommand<ConsoleParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ConsoleParams>(
            CommandNames.Console,
            CommandHostScope.Editor,
            "Read or clear Unity Editor console logs");

        protected override Dictionary<string, object> Execute(ConsoleParams p)
        {
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

        protected override string Message(ConsoleParams p) => p.Clear ? "console cleared" : "console entries";
    }
}

using System.Collections.Generic;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Editor.Services;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    public class ConsoleCommand : CliCommand<ConsoleParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ConsoleParams>(
            CommandNames.Console,
            CommandHostScope.Editor,
            "Read or clear Unity Editor console logs");

        public override void Run(ConsoleParams p)
        {
            if (!UnityConsoleReader.IsReady)
            {
                var error = $"Console reader unavailable: {UnityConsoleReader.InitError}";
                CompleteFail(error);
                return;
            }

            if (p.Clear)
            {
                UnityConsoleReader.Clear();
                CompleteSuccess(InvokeResult.Ok("console cleared"));
                return;
            }

            var stacktrace = string.IsNullOrEmpty(p.Stacktrace) ? "user" : p.Stacktrace;
            var entries = UnityConsoleReader.Read(new UnityConsoleReader.ConsoleReadOptions
            {
                TypeFilter = p.Type ?? "error,warning",
                MaxEntries = p.Lines,
                Stacktrace = stacktrace,
            });

            var data = new Dictionary<string, object>
            {
                ["count"] = entries.Count,
                ["entries"] = entries,
                ["types"] = p.Type ?? "error,warning",
                ["stacktrace"] = stacktrace,
            };
            CompleteSuccess(InvokeResult.Ok("console entries", data));
        }
    }
}

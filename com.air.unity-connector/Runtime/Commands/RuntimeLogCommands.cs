using Air.UnityConnector.Invoke;
using Air.UnityConnector.Params;
using UnityEngine;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    public class DebugRuntimeCommand : CliCommand<RuntimeLogParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<RuntimeLogParams>(
            CommandNames.Debug,
            CommandHostScope.Runtime,
            "Print runtime debug log");

        public override void Run(RuntimeLogParams p)
        {
            var text = RuntimeLogCommandSupport.BuildText(p);
            Debug.Log(text);
            CompleteSuccess(InvokeResult.Ok(text));
        }
    }

    public class WarnRuntimeCommand : CliCommand<RuntimeLogParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<RuntimeLogParams>(
            CommandNames.Warn,
            CommandHostScope.Runtime,
            "Print runtime warning log");

        public override void Run(RuntimeLogParams p)
        {
            var text = RuntimeLogCommandSupport.BuildText(p);
            Debug.LogWarning(text);
            CompleteSuccess(InvokeResult.Ok(text));
        }
    }

    public class ErrorRuntimeCommand : CliCommand<RuntimeLogParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<RuntimeLogParams>(
            CommandNames.Error,
            CommandHostScope.Runtime,
            "Print runtime error log");

        public override void Run(RuntimeLogParams p)
        {
            var text = RuntimeLogCommandSupport.BuildText(p);
            Debug.LogError(text);
            CompleteSuccess(InvokeResult.Ok(text));
        }
    }

    internal static class RuntimeLogCommandSupport
    {
        public static string BuildText(RuntimeLogParams p)
        {
            var message = p?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = "runtime log";

            if (!string.IsNullOrWhiteSpace(p?.Tag))
                return $"[{p.Tag}] {message}";

            return message;
        }

    }

}

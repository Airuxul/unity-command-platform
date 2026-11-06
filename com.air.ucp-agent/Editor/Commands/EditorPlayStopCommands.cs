using System;
using Air.UcpAgent.Completion;
using Air.UcpAgent.Invoke;
using UnityEditor;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class EditorPlayCommand
    {
        public static readonly Type CompletionPolicy = typeof(EnterPlayModePolicy);

        public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor<PlayParams>(
            CommandNames.Play,
            CommandHostScope.Editor,
            "Enter Play Mode (deferred)",
            CompletionPolicy,
            defaultTimeoutMs: 120_000);

        public static object Execute(PlayParams p, CommandContext ctx)
        {
            EditorApplication.EnterPlaymode();
            return null;
        }
    }

    [UcpCommand]
    public static class EditorStopCommand
    {
        public static readonly Type CompletionPolicy = typeof(ExitPlayModePolicy);

        public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor<StopParams>(
            CommandNames.Stop,
            CommandHostScope.Editor,
            "Exit Play Mode (deferred)",
            CompletionPolicy,
            defaultTimeoutMs: 120_000);

        public static object Execute(StopParams p, CommandContext ctx)
        {
            EditorApplication.ExitPlaymode();
            return null;
        }
    }
}

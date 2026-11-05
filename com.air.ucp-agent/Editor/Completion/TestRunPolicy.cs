using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Completion
{
    /// <summary>Finishes test_run once the framework's callbacks have left its outcome in <see cref="TestRunState"/>.</summary>
    public sealed class TestRunPolicy : ICompletionPolicy
    {
        public string Kind => InvokeCompletionCatalog.CompletionTests;

        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out CommandFailure failure)
        {
            result = null;
            failure = null;

            // A PlayMode run reports before the Test Runner leaves Play Mode and restores the scenes;
            // answering then would hand the caller an Editor still on its way back.
            if (state.IsPlaying || state.IsCompiling)
                return false;

            if (!TestRunState.TryTake(command.Id, out var json, out var error))
                return false;

            if (string.IsNullOrEmpty(json))
            {
                failure = new CommandFailure(UcpErrorCodes.CommandFailed, $"The test run stopped: {error}");
                return true;
            }

            var summary = JObject.Parse(json).ToObject<Dictionary<string, object>>();
            var failed = Convert.ToInt32(summary["failed"]);
            var total = Convert.ToInt32(summary["total"]);
            if (failed > 0)
            {
                failure = new CommandFailure(UcpErrorCodes.TestsFailed, $"{failed} of {total} tests failed.",
                    "data.failures lists each one with its message.", summary);
                return true;
            }

            if (total == 0)
            {
                failure = new CommandFailure(UcpErrorCodes.InvalidArgument, "No test matched.",
                    "tests takes full names (Namespace.Class.Method); filter is a regex on them.", summary);
                return true;
            }

            result = InvokeResult.Ok($"{total} tests ran, none failed", summary);
            return true;
        }
    }
}

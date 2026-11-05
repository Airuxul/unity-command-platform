using System;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using UnityEngine;

namespace Air.UcpAgent.Completion
{
    /// <summary>Checks a ui_wait condition every editor tick until it holds or the wait runs out.</summary>
    public sealed class UiWaitPolicy : ICompletionPolicy
    {
        /// <summary>
        /// Gives up this long before the deadline, so the caller reads what the node looked like
        /// rather than ucp-host's bare timeout, which fires at the deadline itself.
        /// </summary>
        const long AnswerMarginMs = 1500;

        public string Kind => InvokeCompletionCatalog.CompletionUiWait;

        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out CommandFailure failure)
        {
            result = null;
            failure = null;

            var condition = string.IsNullOrEmpty(command.CompletionArgs)
                ? null
                : JsonUtility.FromJson<UiInteractionService.WaitCondition>(command.CompletionArgs);
            if (condition == null)
            {
                failure = new CommandFailure(UcpErrorCodes.CommandFailed, $"{CommandNames.UiWait} job has no condition.");
                return true;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var elapsed = now - command.CreatedAtUtcMs;
            if (!state.IsPlaying)
            {
                failure = new CommandFailure(UcpErrorCodes.PlayModeRequired,
                    $"Play Mode ended while waiting for {UiInteractionService.Describe(condition)}.");
                return true;
            }

            var payload = UiInteractionService.Check(condition, elapsed, out var seen);
            if (payload != null)
            {
                result = InvokeResult.Ok(CommandNames.UiWait, payload);
                return true;
            }

            if (command.DeadlineUtcMs > 0)
            {
                var margin = Math.Min(AnswerMarginMs, (command.DeadlineUtcMs - command.CreatedAtUtcMs) / 2);
                if (now >= command.DeadlineUtcMs - margin)
                {
                    failure = new CommandFailure(UcpErrorCodes.Timeout,
                        $"Gave up after {elapsed} ms waiting for {UiInteractionService.Describe(condition)}: {seen}.",
                        "Pass a longer --timeout if it is still coming; ui_snapshot shows what is there now.");
                    return true;
                }
            }

            return false;
        }
    }
}

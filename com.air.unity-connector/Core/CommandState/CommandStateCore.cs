using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityCliConnector
{
    public static class CommandStateCore
    {
        public delegate bool TryCompleteCommand(CommandRecord command, out object result, out string error);
        public const int DefaultOrphanTimeoutMs = 20000;
        public const string OrphanErrorMessage = "Command timed out without progress (20s).";

        public static CommandRecord Create(IDictionary<string, CommandRecord> commands, string command, string completionKind, string requestId, Func<long> utcNow, Action save)
        {
            var now = utcNow();
            var entry = new CommandRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Command = command,
                RequestId = requestId,
                CompletionKind = completionKind,
                Status = CommandStatus.Pending,
                CreatedAtUtcMs = now,
                UpdatedAtUtcMs = now,
            };
            commands[entry.Id] = entry;
            save?.Invoke();
            return entry;
        }

        public static bool ShouldSkip(CommandRecord command) =>
            command.Status is CommandStatus.Succeeded or CommandStatus.Failed or CommandStatus.Orphaned;

        public static bool TryOrphan(CommandRecord command, long nowUtcMs, int orphanTimeoutMs, Action save)
        {
            if (nowUtcMs - command.UpdatedAtUtcMs <= orphanTimeoutMs) return false;
            command.Status = CommandStatus.Orphaned;
            command.Error = OrphanErrorMessage;
            command.UpdatedAtUtcMs = nowUtcMs;
            save?.Invoke();
            return true;
        }

        public static void MarkFailed(CommandRecord command, string error, long nowUtcMs, Action save)
        {
            command.Status = CommandStatus.Failed;
            command.Error = error;
            command.UpdatedAtUtcMs = nowUtcMs;
            save?.Invoke();
        }

        public static void MarkSucceeded(CommandRecord command, object result, long nowUtcMs, bool serializeResultJson, Action save)
        {
            command.Status = CommandStatus.Succeeded;
            command.Result = result;
            if (serializeResultJson) command.ResultJson = result != null ? ConnectorJson.Serialize(result) : "";
            command.UpdatedAtUtcMs = nowUtcMs;
            save?.Invoke();
        }

        public static void TickCommands(IEnumerable<CommandRecord> commands, Action save, Func<CommandRecord, TryCompleteCommand> resolveCompletion, Func<CommandRecord, string> resolveMissingPolicyError, Func<CommandRecord, bool> canRunWithoutPolicy, bool serializeResultJson)
        {
            var now = UtcNowMs();
            foreach (var command in commands.ToList())
            {
                if (ShouldSkip(command)) continue;
                if (TryOrphan(command, now, DefaultOrphanTimeoutMs, save)) continue;
                var tryComplete = resolveCompletion(command);
                if (tryComplete == null)
                {
                    if (canRunWithoutPolicy != null && canRunWithoutPolicy(command))
                    {
                        if (command.Status == CommandStatus.Pending)
                        {
                            command.Status = CommandStatus.Running;
                            command.UpdatedAtUtcMs = now;
                            save?.Invoke();
                        }
                        continue;
                    }

                    MarkFailed(command, resolveMissingPolicyError(command), now, save);
                    continue;
                }

                if (tryComplete(command, out var result, out var error))
                {
                    if (!string.IsNullOrEmpty(error)) MarkFailed(command, error, now, save);
                    else MarkSucceeded(command, result, now, serializeResultJson, save);
                }
                else if (command.Status == CommandStatus.Pending)
                {
                    command.Status = CommandStatus.Running;
                    command.UpdatedAtUtcMs = now;
                    save?.Invoke();
                }
            }
        }

        public static long UtcNowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}

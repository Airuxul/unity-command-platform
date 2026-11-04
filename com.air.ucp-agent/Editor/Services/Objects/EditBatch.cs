using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Editor.Commands;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// The one place an edit target is written: open, run, then commit on success or revert on
    /// failure. A single command is a batch of one, so a failed command never leaves half an edit.
    /// </summary>
    public static class EditBatch
    {
        public const int MaxOps = 100;

        static Dictionary<string, IEditTargetCommand> _commands;

        /// <param name="keep">False runs <paramref name="body"/> for real and then reverts: a dry run.</param>
        public static T Run<T>(EditTargetScope scope, Func<EditTargetScope, T> body, bool keep = true)
        {
            using (scope)
            {
                try
                {
                    var result = body(scope);
                    if (keep)
                        scope.Commit();
                    else
                        scope.Revert();
                    return result;
                }
                catch
                {
                    scope.Revert();
                    throw;
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="ops"/> in order against one target and persists once. Every op is
        /// resolved before the target is opened, so a typo in the last op costs nothing. With
        /// <paramref name="dryRun"/> every op still runs, so the results show what would happen,
        /// and then nothing is kept.
        /// </summary>
        public static Dictionary<string, object> RunOps(
            Func<EditTargetScope> openTarget,
            IReadOnlyList<EditBatchOp> ops,
            bool dryRun)
        {
            var resolved = Resolve(ops);
            var target = openTarget();
            if (dryRun && !target.CanRevert)
            {
                target.Dispose();
                throw new InvalidOperationException(
                    "dry_run needs undo to take the ops back, and Play Mode records none. Stop play first.");
            }

            return Run(target, scope =>
            {
                var results = new List<Dictionary<string, object>>(resolved.Count);
                for (var i = 0; i < resolved.Count; i++)
                {
                    var (command, args) = resolved[i];
                    try
                    {
                        results.Add(new Dictionary<string, object>
                        {
                            ["command"] = command.Name,
                            ["data"] = command.Apply(scope, args),
                        });
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"ops[{i}] {command.Name} failed, so no op was kept: {ex.Message}", ex);
                    }
                }

                return new Dictionary<string, object>
                {
                    ["target"] = scope.Describe(),
                    ["dry_run"] = dryRun,
                    ["results"] = results,
                };
            }, keep: !dryRun);
        }

        static List<(IEditTargetCommand command, object args)> Resolve(IReadOnlyList<EditBatchOp> ops)
        {
            if (ops == null || ops.Count == 0)
                throw new ArgumentException("'ops' must list at least one {\"command\":..,\"args\":{..}}.");
            if (ops.Count > MaxOps)
                throw new ArgumentException($"A batch takes at most {MaxOps} ops, got {ops.Count}.");

            var resolved = new List<(IEditTargetCommand, object)>(ops.Count);
            for (var i = 0; i < ops.Count; i++)
            {
                var name = ops[i]?.Command?.Trim();
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException($"ops[{i}] has no 'command'.");

                if (!Commands().TryGetValue(name, out var command) || !command.Batchable)
                {
                    throw new ArgumentException(
                        $"ops[{i}] '{name}' cannot run in a batch. Batchable: {string.Join(", ", BatchableNames())}.");
                }

                var values = ops[i].Args?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                var (args, error) = InvokeParameterBinding.BindRequired(command.ParamsType, values);
                if (error != null)
                    throw new ArgumentException($"ops[{i}] {name}: {error}");

                var target = (EditTargetParams)args;
                if (!string.IsNullOrWhiteSpace(target.Prefab) || !string.IsNullOrWhiteSpace(target.Scene))
                    throw new ArgumentException($"ops[{i}] {name}: 'prefab' / 'scene' belong on the batch, not on an op.");

                resolved.Add((command, args));
            }

            return resolved;
        }

        static IEnumerable<string> BatchableNames() =>
            Commands().Values.Where(c => c.Batchable).Select(c => c.Name).Distinct().OrderBy(n => n);

        static Dictionary<string, IEditTargetCommand> Commands()
        {
            if (_commands != null)
                return _commands;

            var map = new Dictionary<string, IEditTargetCommand>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in typeof(EditBatch).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(IEditTargetCommand).IsAssignableFrom(type))
                    continue;

                var command = (IEditTargetCommand)Activator.CreateInstance(type);
                map[command.Name] = command;
                foreach (var alias in command.Aliases)
                    map[alias] = command;
            }

            return _commands = map;
        }
    }

    public sealed class EditBatchOp
    {
        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("args")]
        public JObject Args { get; set; }
    }
}

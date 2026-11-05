using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Commands;
using Air.UcpAgent.Cli;
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
                catch (Exception ex)
                {
                    try
                    {
                        scope.Revert();
                    }
                    catch (Exception revertError)
                    {
                        // The caller needs the reason the edit failed, not only the rollback's.
                        throw UcpException.Wrap(
                            ex,
                            $"{ex.Message} Rolling the edit back also failed, so the target may be half-edited: {revertError.Message}");
                    }

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
                throw new UcpException(
                    UcpErrorCodes.EditModeRequired,
                    "dry_run needs undo to take the ops back, and Play Mode records none.",
                    $"Run {CommandNames.Stop} first.");
            }

            return Run(target, scope =>
            {
                var results = new List<Dictionary<string, object>>(resolved.Count);
                for (var i = 0; i < resolved.Count; i++)
                {
                    var (command, args, expect) = resolved[i];
                    // Checked here rather than up front: the ops before this one have run.
                    expect?.Check(scope, i, command.Name);
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
                        throw UcpException.Wrap(ex, $"ops[{i}] {command.Name} failed, so no op was kept: {ex.Message}");
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

        static List<(IEditTargetCommand command, object args, OpExpectation expect)> Resolve(IReadOnlyList<EditBatchOp> ops)
        {
            if (ops == null || ops.Count == 0)
                throw new ArgumentException("'ops' must list at least one {\"command\":..,\"args\":{..}}.");
            if (ops.Count > MaxOps)
                throw new ArgumentException($"A batch takes at most {MaxOps} ops, got {ops.Count}.");

            var resolved = new List<(IEditTargetCommand, object, OpExpectation)>(ops.Count);
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
                var (args, error) = CliParamBinder.Bind(command.ParamsType, values);
                if (error != null)
                    throw new ArgumentException($"ops[{i}] {name}: {error}");

                var target = (EditTargetParams)args;
                if (!string.IsNullOrWhiteSpace(target.Prefab) || !string.IsNullOrWhiteSpace(target.Scene))
                    throw new ArgumentException($"ops[{i}] {name}: '{ParamKeys.Prefab}' / '{ParamKeys.Scene}' belong on the batch, not on an op.");

                var expect = OpExpectation.Parse(ops[i].Expect, values, i, name);
                resolved.Add((command, args, expect));
            }

            return resolved;
        }

        static IEnumerable<string> BatchableNames() =>
            Commands().Values.Where(c => c.Batchable).Select(c => c.Name).Distinct().OrderBy(n => n);

        /// <summary>
        /// The registry's Editor commands that work inside one edit target, keyed by name and alias.
        /// </summary>
        static Dictionary<string, IEditTargetCommand> Commands()
        {
            if (_commands != null)
                return _commands;

            var map = new Dictionary<string, IEditTargetCommand>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in CommandRegistry.ForHost(CommandRegistry.EditorHost))
            {
                if (!typeof(IEditTargetCommand).IsAssignableFrom(entry.CommandType))
                    continue;

                var command = (IEditTargetCommand)entry.Create();
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

        /// <summary>Optional <c>{node?, exists?, components?}</c>, checked just before the op runs.</summary>
        [JsonProperty("expect")]
        public JObject Expect { get; set; }
    }

    /// <summary>
    /// A batch op's precondition. A false one rolls the whole batch back, so an op never acts on
    /// a hierarchy that differs from what the caller last read.
    /// </summary>
    public sealed class OpExpectation
    {
        public const string NodeKey = "node";
        public const string ExistsKey = "exists";
        public const string ComponentsKey = "components";
        static readonly string[] Keys = { NodeKey, ExistsKey, ComponentsKey };

        string _node;
        bool? _exists;
        List<(string name, Type type)> _components;

        /// <summary>Null when the op has no expect. The node defaults to the op's own <c>node</c>.</summary>
        public static OpExpectation Parse(JObject expect, Dictionary<string, object> opArgs, int index, string opName)
        {
            if (expect == null)
                return null;

            var where = $"ops[{index}] {opName} expect";
            foreach (var property in expect.Properties())
            {
                if (Array.IndexOf(Keys, property.Name) < 0)
                    throw new ArgumentException($"{where}: unknown key '{property.Name}'. Valid: {string.Join(", ", Keys)}.");
            }

            var result = new OpExpectation();
            try
            {
                result._node = expect.Value<string>(NodeKey);
                result._exists = expect.Value<bool?>(ExistsKey);
                var components = expect[ComponentsKey]?.ToObject<string[]>();
                result._components = components?
                    .Select(c => (c, ComponentTypeResolver.RequireComponent(c)))
                    .ToList();
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or JsonException)
            {
                throw new ArgumentException($"{where}: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(result._node) && opArgs.TryGetValue(ParamKeys.Node, out var own))
                result._node = own as string;
            if (string.IsNullOrWhiteSpace(result._node))
                throw new ArgumentException($"{where}: pass '{NodeKey}', the op has none of its own.");
            if (result._exists == null && result._components == null)
                throw new ArgumentException($"{where}: pass '{ExistsKey}' and/or '{ComponentsKey}'.");
            if (result._exists == false && result._components != null)
                throw new ArgumentException($"{where}: '{ComponentsKey}' cannot hold on a node expected not to exist.");

            return result;
        }

        public void Check(EditTargetScope scope, int index, string opName)
        {
            var node = GameObjectPath.Resolve(scope, _node);
            var exists = node != null;
            if (_exists != null && _exists.Value != exists)
                Fail(index, opName, exists ? $"'{_node}' exists, expected it not to." : $"'{_node}' does not exist.");

            if (_components == null)
                return;
            if (!exists)
                Fail(index, opName, $"'{_node}' does not exist, so it has none of {string.Join(", ", _components.Select(c => c.name))}.");

            var missing = _components.Where(c => node.GetComponent(c.type) == null).Select(c => c.name).ToList();
            if (missing.Count > 0)
            {
                Fail(index, opName, $"'{_node}' has no {string.Join(", ", missing)}. " +
                    $"It has: {string.Join(", ", ComponentService.Names(node.gameObject))}.");
            }
        }

        static void Fail(int index, string opName, string what) =>
            throw new UcpException(
                UcpErrorCodes.ExpectationFailed,
                $"ops[{index}] {opName} expect failed, so no op was kept: {what}");
    }
}

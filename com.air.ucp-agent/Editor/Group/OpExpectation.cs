using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Editor.Group
{
    /// <summary>
    /// A group step's precondition, checked once its target is open. A false one fails the step,
    /// so a step never acts on a hierarchy that differs from what the caller last read.
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

            var where = $"steps[{index}] {opName} expect";
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
                $"steps[{index}] {opName} expect failed, so no op was kept: {what}");
    }
}

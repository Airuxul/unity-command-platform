using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Params;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Component add/remove/read/write against an <see cref="EditTargetScope"/>, so the same five
    /// commands work on scene objects and on nodes inside a prefab asset.
    /// </summary>
    public static class ComponentService
    {
        public static Dictionary<string, object> Add(ComponentAddParams p)
        {
            using var scope = NodeTargets.Open(p);
            // Hold the GameObject, not its Transform: adding a component that requires a
            // RectTransform (any UI component) makes Unity swap the Transform instance, leaving a
            // cached reference pointing at a destroyed object.
            var target = NodeTargets.Require(scope, p).gameObject;
            var type = ComponentTypeResolver.RequireComponent(p.Component);

            if (type == typeof(Transform) || type == typeof(RectTransform))
            {
                throw new InvalidOperationException(
                    $"{type.Name} cannot be added — every GameObject already has one. Set its properties with component_set.");
            }

            var component = scope.SupportsUndo
                ? Undo.AddComponent(target, type)
                : target.AddComponent(type);

            if (component == null)
            {
                throw new InvalidOperationException(
                    $"Unity refused to add {type.Name} to '{GameObjectPath.Of(target.transform)}' — check its RequireComponent / DisallowMultipleComponent constraints.");
            }

            var applied = SerializedValueWriter.Apply(component, p.Properties, NodeTargets.WriterContext(scope));
            scope.MarkDirty();

            return GameObjectService.Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(target.transform),
                ["component"] = type.Name,
                ["full_type"] = type.FullName,
                ["index"] = IndexOf(target, component),
                ["properties_set"] = applied,
                ["rect_transform"] = target.transform is RectTransform,
            });
        }

        public static Dictionary<string, object> Remove(ComponentRemoveParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var component = Require(node.gameObject, p.Component, p.Index);

            if (component is Transform)
                throw new InvalidOperationException("Transform cannot be removed from a GameObject.");

            var typeName = component.GetType().Name;
            NodeTargets.Destroy(scope, component);
            scope.MarkDirty();

            return GameObjectService.Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["component"] = typeName,
                ["removed"] = true,
                ["remaining"] = NodeTargets.ComponentNames(node.gameObject),
            });
        }

        public static Dictionary<string, object> List(ComponentListParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);

            var components = new List<object>();
            var all = node.gameObject.GetComponents<Component>();
            for (var i = 0; i < all.Length; i++)
            {
                var component = all[i];
                if (component == null)
                {
                    components.Add(new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["type"] = "<missing script>",
                    });
                    continue;
                }

                var entry = new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["type"] = component.GetType().Name,
                    ["full_type"] = component.GetType().FullName,
                    ["instance_id"] = component.GetInstanceID(),
                };

                if (component is Behaviour behaviour)
                    entry["enabled"] = behaviour.enabled;

                components.Add(entry);
            }

            return GameObjectService.Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["count"] = all.Length,
                ["components"] = components,
            });
        }

        public static Dictionary<string, object> Get(ComponentGetParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var component = Require(node.gameObject, p.Component, p.Index);

            var options = new SerializedValueReader.Options
            {
                MaxDepth = p.Depth ?? 2,
                MaxArrayElements = p.MaxArray ?? 64,
                IncludeInternal = p.Internal,
            };

            object properties;
            if (string.IsNullOrWhiteSpace(p.Property))
            {
                properties = SerializedValueReader.Read(component, options);
            }
            else
            {
                var serialized = new SerializedObject(component);
                var property = SerializedValueWriter.FindProperty(serialized, p.Property);
                if (property == null)
                {
                    throw new ArgumentException(
                        $"{component.GetType().Name} has no property '{p.Property}'. " +
                        "Omit 'property' to list everything it does have.");
                }

                properties = new Dictionary<string, object>
                {
                    [property.name] = SerializedValueReader.ReadProperty(property, options, 0),
                };
            }

            return GameObjectService.Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["component"] = component.GetType().Name,
                ["full_type"] = component.GetType().FullName,
                ["properties"] = properties,
            });
        }

        public static Dictionary<string, object> Set(ComponentSetParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var component = Require(node.gameObject, p.Component, p.Index);

            var properties = p.Properties;
            if (!string.IsNullOrWhiteSpace(p.Property))
            {
                properties ??= new JObject();
                properties[p.Property] = p.Value ?? JValue.CreateNull();
            }

            if (properties == null || properties.Count == 0)
            {
                throw new ArgumentException(
                    "Pass 'properties' as an object, or 'property' plus 'value' for a single field.");
            }

            NodeTargets.RecordUndo(scope, component, "ucp component_set");
            var applied = SerializedValueWriter.Apply(component, properties, NodeTargets.WriterContext(scope));
            scope.MarkDirty();

            return GameObjectService.Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["component"] = component.GetType().Name,
                ["properties_set"] = applied,
                ["count"] = applied.Count,
            });
        }

        /// <summary>
        /// Resolves a component by type name, with <paramref name="index"/> choosing among repeats.
        /// </summary>
        public static Component Require(GameObject node, string typeName, int? index)
        {
            var type = ComponentTypeResolver.RequireComponent(typeName);
            var matches = node.GetComponents(type).Where(c => c != null).ToArray();

            if (matches.Length == 0)
            {
                throw new ArgumentException(
                    $"'{GameObjectPath.Of(node.transform)}' has no {type.Name}. " +
                    $"It has: {string.Join(", ", NodeTargets.ComponentNames(node))}.");
            }

            var wanted = index ?? 0;
            if (wanted < 0 || wanted >= matches.Length)
            {
                throw new ArgumentException(
                    $"'{GameObjectPath.Of(node.transform)}' has {matches.Length} {type.Name} component(s); index {wanted} is out of range.");
            }

            return matches[wanted];
        }

        static int IndexOf(GameObject node, Component component) =>
            Array.IndexOf(node.GetComponents<Component>(), component);
    }
}

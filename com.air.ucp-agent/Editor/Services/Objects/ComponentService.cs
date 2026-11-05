using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Commands;
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
        public static Dictionary<string, object> Add(EditTargetScope scope, ComponentAddParams p)
        {
            // Hold the GameObject, not its Transform: adding a component that requires a
            // RectTransform (any UI component) makes Unity swap the Transform instance, leaving a
            // cached reference pointing at a destroyed object.
            var target = GameObjectPath.Require(scope, p).gameObject;
            var type = ComponentTypeResolver.RequireComponent(p.Component);

            if (type == typeof(Transform) || type == typeof(RectTransform))
            {
                throw new InvalidOperationException(
                    $"{type.Name} cannot be added — every GameObject already has one. Set its properties with {CommandNames.ComponentSet}.");
            }

            var component = scope.SupportsUndo
                ? Undo.AddComponent(target, type)
                : target.AddComponent(type);

            if (component == null)
            {
                throw new InvalidOperationException(
                    $"Unity refused to add {type.Name} to '{GameObjectPath.Of(target.transform)}' — check its RequireComponent / DisallowMultipleComponent constraints.");
            }

            var applied = SerializedValueWriter.Apply(component, p.Properties, scope);
            scope.MarkDirty();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(target.transform),
                ["component"] = type.Name,
                ["full_type"] = type.FullName,
                ["index"] = Array.IndexOf(target.GetComponents<Component>(), component),
                ["properties_set"] = applied,
                ["rect_transform"] = target.transform is RectTransform,
            };
        }

        public static Dictionary<string, object> Remove(EditTargetScope scope, ComponentRemoveParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var component = Require(node.gameObject, p.Component, p.Index);

            if (component is Transform)
                throw new InvalidOperationException("Transform cannot be removed from a GameObject.");

            var typeName = component.GetType().Name;
            scope.Destroy(component);
            scope.MarkDirty();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["component"] = typeName,
                ["removed"] = true,
                ["remaining"] = Names(node.gameObject),
            };
        }

        public static Dictionary<string, object> List(EditTargetScope scope, ComponentListParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var components = Describe(node.gameObject);

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["count"] = components.Count,
                ["components"] = components,
            };
        }

        public static Dictionary<string, object> Get(EditTargetScope scope, ComponentGetParams p)
        {
            var node = GameObjectPath.Require(scope, p);
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
                var property = RequireProperty(component, p.Property);
                properties = new Dictionary<string, object>
                {
                    [property.name] = SerializedValueReader.ReadProperty(property, options, 0),
                };
            }

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["component"] = component.GetType().Name,
                ["full_type"] = component.GetType().FullName,
                ["properties"] = properties,
            };
        }

        public static Dictionary<string, object> Set(EditTargetScope scope, ComponentSetParams p)
        {
            var node = GameObjectPath.Require(scope, p);
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

            scope.RecordUndo(component, $"ucp {CommandNames.ComponentSet}");
            var applied = SerializedValueWriter.Apply(component, properties, scope);
            scope.MarkDirty();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["component"] = component.GetType().Name,
                ["properties_set"] = applied,
                ["count"] = applied.Count,
            };
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
                    $"It has: {string.Join(", ", Names(node))}.");
            }

            var wanted = index ?? 0;
            if (wanted < 0 || wanted >= matches.Length)
            {
                throw new ArgumentException(
                    $"'{GameObjectPath.Of(node.transform)}' has {matches.Length} {type.Name} component(s); index {wanted} is out of range.");
            }

            return matches[wanted];
        }

        /// <summary>One serialized field of <paramref name="component"/>, found the way component_set finds it.</summary>
        public static SerializedProperty RequireProperty(Component component, string name)
        {
            var property = SerializedValueWriter.FindProperty(new SerializedObject(component), name);
            if (property == null)
            {
                throw new ArgumentException(
                    $"{component.GetType().Name} has no property '{name}'. " +
                    $"{CommandNames.ComponentGet} without 'property' lists everything it does have.");
            }

            return property;
        }

        /// <summary>Component type names on a node, with missing scripts called out rather than dropped.</summary>
        public static List<string> Names(GameObject node) =>
            node.GetComponents<Component>()
                .Select(c => c == null ? "<missing script>" : c.GetType().Name)
                .ToList();

        /// <summary>One entry per component slot, in order; a missing script keeps its slot.</summary>
        public static List<object> Describe(GameObject node)
        {
            var components = new List<object>();
            var all = node.GetComponents<Component>();
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

            return components;
        }
    }
}

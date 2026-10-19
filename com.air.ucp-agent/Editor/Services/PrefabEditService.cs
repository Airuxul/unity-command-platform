using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services
{
    public static class PrefabEditService
    {
        public static Dictionary<string, object> Read(string prefabPath)
        {
            prefabPath = Normalize(prefabPath);
            EnsureEditMode();
            var stageOpen = StageOpen(prefabPath);
            var root = LoadContents(prefabPath);
            try
            {
                return new Dictionary<string, object>
                {
                    ["prefab_path"] = prefabPath,
                    ["prefab_stage_open"] = stageOpen,
                    ["nodes"] = Flatten(root.transform),
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static Dictionary<string, object> Edit(string prefabPath, object opsValue)
        {
            prefabPath = Normalize(prefabPath);
            EnsureEditMode();
            if (StageOpen(prefabPath))
                throw new InvalidOperationException(
                    "This prefab is open in Prefab Mode. Exit Prefab Mode before prefab.edit, or the two saves will overwrite each other.");

            var ops = ParseOps(opsValue);
            var root = LoadContents(prefabPath);
            try
            {
                var done = new List<Dictionary<string, object>>();
                foreach (var op in ops)
                    done.Add(Apply(root, op));
                EditorUtility.SetDirty(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var success);
                if (!success || saved == null)
                    throw new InvalidOperationException($"Failed to save prefab: {prefabPath}");
                return new Dictionary<string, object>
                {
                    ["prefab_path"] = prefabPath,
                    ["done"] = done,
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static List<Dictionary<string, object>> Flatten(Transform root)
        {
            var nodes = new List<Dictionary<string, object>>();
            Collect(root, ".", nodes);
            return nodes;
        }

        static void Collect(Transform current, string path, List<Dictionary<string, object>> nodes)
        {
            nodes.Add(new Dictionary<string, object>
            {
                ["path"] = path,
                ["name"] = current.name,
                ["components"] = ComponentNames(current),
            });

            var nameCount = new Dictionary<string, int>();
            for (int i = 0; i < current.childCount; i++)
            {
                var childName = current.GetChild(i).name;
                nameCount.TryGetValue(childName, out var count);
                nameCount[childName] = count + 1;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                var segment = nameCount[child.name] > 1 ? "#" + (i + 1) : child.name;
                var childPath = path == "." ? segment : path + "/" + segment;
                Collect(child, childPath, nodes);
            }
        }

        static List<string> ComponentNames(Transform node)
        {
            var names = new List<string>();
            var components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                names.Add(component == null ? "(missing script)" : component.GetType().FullName);
            }

            return names;
        }

        static Dictionary<string, object> Apply(GameObject root, PrefabEditOp op)
        {
            var action = op.Action?.Trim().ToLowerInvariant();
            var node = Find(root.transform, op.Node);
            if (node == null)
                throw new InvalidOperationException($"Node not found: {op.Node}. Run prefab.read and pass a path from that list.");

            if (action == "add_component")
            {
                var type = ResolveComponent(op.Type);
                var added = node.GetComponent(type) == null;
                if (added)
                    node.gameObject.AddComponent(type);
                return new Dictionary<string, object>
                {
                    ["action"] = action,
                    ["node"] = op.Node,
                    ["type"] = type.FullName,
                    ["added"] = added,
                };
            }

            if (action == "set_reference")
            {
                var type = ResolveComponent(op.Type);
                var component = node.GetComponent(type);
                if (component == null)
                    throw new InvalidOperationException($"{op.Node} has no {type.FullName}.");
                var targetNode = Find(root.transform, op.Target);
                if (targetNode == null)
                    throw new InvalidOperationException($"Target node not found: {op.Target}.");
                UnityEngine.Object target = targetNode.gameObject;
                string targetTypeName = null;
                if (!string.IsNullOrWhiteSpace(op.TargetType))
                {
                    var targetType = ResolveComponent(op.TargetType);
                    target = targetNode.GetComponent(targetType);
                    if (target == null)
                        throw new InvalidOperationException($"{op.Target} has no {targetType.FullName}.");
                    targetTypeName = targetType.FullName;
                }

                var so = new SerializedObject(component);
                var prop = so.FindProperty(op.Field);
                if (prop == null)
                    throw new InvalidOperationException($"{type.FullName} has no field {op.Field}.");
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    throw new InvalidOperationException($"{type.FullName}.{op.Field} is not an object reference.");
                prop.objectReferenceValue = target;
                if (prop.objectReferenceValue != target)
                    throw new InvalidOperationException(
                        $"{type.FullName}.{op.Field} cannot store a {target.GetType().FullName} reference (serialized type {prop.type}).");
                so.ApplyModifiedPropertiesWithoutUndo();
                if (prop.objectReferenceValue != target)
                    throw new InvalidOperationException($"{type.FullName}.{op.Field} was not written.");
                EditorUtility.SetDirty(component);
                var result = new Dictionary<string, object>
                {
                    ["action"] = action,
                    ["node"] = op.Node,
                    ["type"] = type.FullName,
                    ["field"] = op.Field,
                    ["target"] = op.Target,
                };
                if (targetTypeName != null)
                    result["target_type"] = targetTypeName;
                return result;
            }

            if (action == "rename")
            {
                if (string.IsNullOrWhiteSpace(op.Name))
                    throw new ArgumentException("rename requires name.");
                node.name = op.Name;
                return new Dictionary<string, object>
                {
                    ["action"] = action,
                    ["node"] = op.Node,
                    ["name"] = op.Name,
                };
            }

            throw new ArgumentException($"Unknown prefab action: {op.Action}. Use add_component, set_reference, or rename.");
        }

        static Transform Find(Transform root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == ".")
                return root;
            var parts = path.Split('/');
            return Walk(root, parts, 0);
        }

        static Transform Walk(Transform current, string[] parts, int index)
        {
            if (index == parts.Length)
                return current;
            if (index == 0 && !IsChildIndex(parts[0]) && current.name == parts[0])
                return Walk(current, parts, 1);
            if (TryChildIndex(parts[index], out var childIndex))
            {
                if (childIndex < 0 || childIndex >= current.childCount)
                    return null;
                return Walk(current.GetChild(childIndex), parts, index + 1);
            }

            for (int i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                if (child.name != parts[index])
                    continue;
                var found = Walk(child, parts, index + 1);
                if (found != null)
                    return found;
            }

            return null;
        }

        static bool IsChildIndex(string part) => TryChildIndex(part, out _);

        static bool TryChildIndex(string part, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(part) || part[0] != '#' || part.Length < 2)
                return false;
            for (int i = 1; i < part.Length; i++)
            {
                if (part[i] < '0' || part[i] > '9')
                    return false;
            }

            if (!int.TryParse(part.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var nth))
                return false;
            index = nth - 1;
            return true;
        }

        static Type ResolveComponent(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("type is required.");

            if (name.IndexOf('.') >= 0)
            {
                var exact = FindExact(name);
                if (exact == null)
                    throw new InvalidOperationException($"Component type not found: {name}.");
                return RequireConcreteComponent(exact);
            }

            var matches = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (var candidate in types)
                {
                    if (candidate == null || candidate.Name != name || !IsConcreteComponent(candidate))
                        continue;
                    if (!matches.Contains(candidate))
                        matches.Add(candidate);
                }
            }

            if (matches.Count == 0)
                throw new InvalidOperationException(
                    $"No component named {name}. Use a full name such as UnityEngine.UI.{name}.");
            if (matches.Count == 1)
                return matches[0];

            var ui = matches.FindAll(type => type.Namespace == "UnityEngine.UI");
            if (ui.Count == 1)
                return ui[0];

            throw new InvalidOperationException(
                $"Short name {name} matches more than one component: {Join(matches)}. Use a full name.");
        }

        static Type FindExact(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        static Type RequireConcreteComponent(Type type)
        {
            if (!typeof(Component).IsAssignableFrom(type))
                throw new InvalidOperationException($"{type.FullName} is not a component.");
            if (!IsConcreteComponent(type))
                throw new InvalidOperationException($"{type.FullName} is abstract and cannot be added.");
            return type;
        }

        static bool IsConcreteComponent(Type type)
        {
            return typeof(Component).IsAssignableFrom(type) && !type.IsAbstract && !type.IsGenericTypeDefinition;
        }

        static string Join(List<Type> types)
        {
            var names = new string[types.Count];
            for (int i = 0; i < types.Count; i++)
                names[i] = types[i].FullName;
            return string.Join(", ", names);
        }

        static GameObject LoadContents(string prefabPath)
        {
            if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"prefab_path must end with .prefab: {prefabPath}");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
                throw new InvalidOperationException($"Prefab not found: {prefabPath}");
            if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab)
                throw new InvalidOperationException($"Not a prefab asset: {prefabPath}");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                throw new InvalidOperationException($"Failed to load prefab: {prefabPath}");
            return root;
        }

        static string Normalize(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                throw new ArgumentException("prefab_path is required.");
            return prefabPath.Replace('\\', '/');
        }

        static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "Prefab commands run in edit mode only. Saving during play would write runtime state into the prefab.");
        }

        static bool StageOpen(string prefabPath)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null &&
                   string.Equals(stage.assetPath.Replace('\\', '/'), prefabPath, StringComparison.OrdinalIgnoreCase);
        }

        static List<PrefabEditOp> ParseOps(object opsValue)
        {
            if (opsValue == null || (opsValue is string text && string.IsNullOrWhiteSpace(text)))
                throw new ArgumentException("ops is required.");

            var json = opsValue is string s ? s : JsonConvert.SerializeObject(opsValue);
            var ops = JsonConvert.DeserializeObject<List<PrefabEditOp>>(json);
            if (ops == null || ops.Count == 0)
                throw new ArgumentException("ops must be a non-empty JSON array.");
            return ops;
        }
    }

    public sealed class PrefabEditOp
    {
        [JsonProperty("action")] public string Action { get; set; }
        [JsonProperty("node")] public string Node { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("field")] public string Field { get; set; }
        [JsonProperty("target")] public string Target { get; set; }
        [JsonProperty("target_type")] public string TargetType { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }
}

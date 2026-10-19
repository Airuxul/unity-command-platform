using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services
{
    public sealed class HierarchyTarget
    {
        readonly Transform _prefabRoot;

        HierarchyTarget(Transform prefabRoot) => _prefabRoot = prefabRoot;

        public static HierarchyTarget Prefab(Transform root) => new HierarchyTarget(root);

        public static HierarchyTarget Scene() => new HierarchyTarget(null);

        public bool IsScene => _prefabRoot == null;

        public Transform Find(string path)
        {
            if (IsScene)
                return HierarchyEdit.FindScene(path);
            return HierarchyEdit.Find(_prefabRoot, path);
        }

        public Transform Require(string path)
        {
            var node = Find(path);
            if (node == null)
                throw new InvalidOperationException(
                    $"Node not found: {path}. Run prefab.read or scene.read and pass a path from that list.");
            return node;
        }

        public Transform Parent(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == ".")
            {
                if (IsScene)
                    return null;
                return _prefabRoot;
            }

            return Require(path);
        }

        public bool IsPrefabRoot(Transform node) => _prefabRoot != null && node == _prefabRoot;

        public string PathOf(Transform node) => HierarchyEdit.PathOf(node, _prefabRoot);
    }

    public static class HierarchyEdit
    {
        public const string Actions =
            "add_component, set_reference, rename, create_node, delete_node, delete_component, set_property, set_rect, reparent, instantiate";

        public static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "This command runs in edit mode only. Saving during play would write runtime state into the asset.");
        }

        public static List<HierarchyOp> ParseOps(object opsValue)
        {
            if (opsValue == null || (opsValue is string text && string.IsNullOrWhiteSpace(text)))
                throw new ArgumentException("ops is required.");

            var json = opsValue is string s ? s : JsonConvert.SerializeObject(opsValue);
            var ops = JsonConvert.DeserializeObject<List<HierarchyOp>>(json);
            if (ops == null || ops.Count == 0)
                throw new ArgumentException("ops must be a non-empty JSON array.");
            return ops;
        }

        public static List<Dictionary<string, object>> ApplyAll(HierarchyTarget target, object opsValue)
        {
            var ops = ParseOps(opsValue);
            var done = new List<Dictionary<string, object>>(ops.Count);
            foreach (var op in ops)
                done.Add(Apply(target, op));
            return done;
        }

        public static List<Dictionary<string, object>> Flatten(Transform root)
        {
            var nodes = new List<Dictionary<string, object>>();
            Collect(root, ".", nodes);
            return nodes;
        }

        public static List<Dictionary<string, object>> FlattenScene()
        {
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            var nodes = new List<Dictionary<string, object>>();
            var nameCount = CountNames(roots);
            for (int i = 0; i < roots.Length; i++)
            {
                var segment = nameCount[roots[i].name] > 1 ? "#" + (i + 1) : roots[i].name;
                Collect(roots[i].transform, segment, nodes);
            }

            return nodes;
        }

        public static Dictionary<string, object> Describe(Transform node, string path)
        {
            var data = Summary(node, path);
            if (node is RectTransform rect)
                data["rect"] = RectData(rect);
            data["fields"] = Fields(node);
            return data;
        }

        public static Transform Find(Transform root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == ".")
                return root;
            return Walk(root, path.Split('/'), 0);
        }

        public static Transform FindScene(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == ".")
                return null;

            var parts = path.Split('/');
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            Transform current = null;
            if (TryChildIndex(parts[0], out var childIndex))
            {
                if (childIndex < 0 || childIndex >= roots.Length)
                    return null;
                current = roots[childIndex].transform;
            }
            else
            {
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name != parts[0])
                        continue;
                    current = roots[i].transform;
                    break;
                }
            }

            if (current == null)
                return null;
            return Walk(current, parts, 1);
        }

        public static string PathOf(Transform node, Transform prefabRoot)
        {
            if (node == null)
                return null;
            if (prefabRoot != null && node == prefabRoot)
                return ".";

            var parts = new List<string>();
            var current = node;
            while (current != null && current != prefabRoot)
            {
                parts.Add(SegmentOf(current));
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        static Dictionary<string, object> Apply(HierarchyTarget target, HierarchyOp op)
        {
            var action = op.Action?.Trim().ToLowerInvariant();
            if (action == "create_node")
                return CreateNode(target, op);
            if (action == "instantiate")
                return Instantiate(target, op);
            if (action == "reparent")
                return Reparent(target, op);

            var node = target.Require(op.Node);
            if (action == "add_component")
                return AddComponent(op, node);
            if (action == "set_reference")
                return SetReference(target, op, node);
            if (action == "rename")
                return Rename(op, node);
            if (action == "delete_node")
                return DeleteNode(target, op, node);
            if (action == "delete_component")
                return DeleteComponent(op, node);
            if (action == "set_property")
                return SetProperty(op, node);
            if (action == "set_rect")
                return SetRect(op, node);

            throw new ArgumentException($"Unknown action: {op.Action}. Use {Actions}.");
        }

        static Dictionary<string, object> CreateNode(HierarchyTarget target, HierarchyOp op)
        {
            RequirePlainName(op.Name, "create_node");

            var parent = target.Parent(op.Parent);
            var underUi = parent != null && parent.GetComponent<RectTransform>() != null;
            var go = underUi
                ? new GameObject(op.Name, typeof(RectTransform))
                : new GameObject(op.Name);
            if (parent != null)
                go.transform.SetParent(parent, false);

            return new Dictionary<string, object>
            {
                ["action"] = "create_node",
                ["parent"] = string.IsNullOrWhiteSpace(op.Parent) ? "." : op.Parent,
                ["name"] = op.Name,
                ["path"] = target.PathOf(go.transform),
            };
        }

        static Dictionary<string, object> Instantiate(HierarchyTarget target, HierarchyOp op)
        {
            if (string.IsNullOrWhiteSpace(op.PrefabPath))
                throw new ArgumentException("instantiate requires prefab_path.");

            var path = op.PrefabPath.Replace('\\', '/');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException($"Prefab not found: {path}");

            var parent = target.Parent(op.Parent);
            var scene = parent != null ? parent.gameObject.scene : EditorSceneManager.GetActiveScene();
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Failed to instantiate prefab: {path}");
            if (parent != null)
                instance.transform.SetParent(parent, false);
            if (!string.IsNullOrWhiteSpace(op.Name))
            {
                RequirePlainName(op.Name, "instantiate");
                instance.name = op.Name;
            }

            return new Dictionary<string, object>
            {
                ["action"] = "instantiate",
                ["parent"] = string.IsNullOrWhiteSpace(op.Parent) ? "." : op.Parent,
                ["prefab_path"] = path,
                ["path"] = target.PathOf(instance.transform),
            };
        }

        static Dictionary<string, object> Reparent(HierarchyTarget target, HierarchyOp op)
        {
            var node = target.Require(op.Node);
            if (target.IsPrefabRoot(node))
                throw new InvalidOperationException("Cannot reparent the prefab root.");

            var parent = target.Parent(op.Parent);
            if (parent != null && (parent == node || parent.IsChildOf(node)))
                throw new InvalidOperationException("Cannot parent a node under itself.");

            node.SetParent(parent, false);
            if (op.Sibling is int sibling)
            {
                if (sibling < 1)
                    throw new ArgumentException("sibling is the 1-based child index.");
                var count = parent == null
                    ? EditorSceneManager.GetActiveScene().rootCount
                    : parent.childCount;
                if (sibling > count)
                    throw new ArgumentException($"sibling {sibling} is outside 1..{count}.");
                node.SetSiblingIndex(sibling - 1);
            }

            return new Dictionary<string, object>
            {
                ["action"] = "reparent",
                ["node"] = op.Node,
                ["parent"] = string.IsNullOrWhiteSpace(op.Parent) ? "." : op.Parent,
                ["path"] = target.PathOf(node),
            };
        }

        static Dictionary<string, object> AddComponent(HierarchyOp op, Transform node)
        {
            var type = ResolveComponent(op.Type);
            var added = node.GetComponent(type) == null;
            if (added)
                node.gameObject.AddComponent(type);
            return new Dictionary<string, object>
            {
                ["action"] = "add_component",
                ["node"] = op.Node,
                ["type"] = type.FullName,
                ["added"] = added,
            };
        }

        static Dictionary<string, object> SetReference(HierarchyTarget target, HierarchyOp op, Transform node)
        {
            var type = ResolveComponent(op.Type);
            var component = node.GetComponent(type);
            if (component == null)
                throw new InvalidOperationException($"{op.Node} has no {type.FullName}.");

            UnityEngine.Object reference = ResolveReference(target, op, out var targetTypeName);
            var so = new SerializedObject(component);
            var prop = FindField(so, op.Field);
            if (prop == null)
                throw new InvalidOperationException($"{type.FullName} has no field {op.Field}.");
            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException($"{type.FullName}.{op.Field} is not an object reference.");
            prop.objectReferenceValue = reference;
            if (prop.objectReferenceValue != reference)
                throw new InvalidOperationException(
                    $"{type.FullName}.{op.Field} cannot store a {reference.GetType().FullName} reference (serialized type {prop.type}).");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);

            var result = new Dictionary<string, object>
            {
                ["action"] = "set_reference",
                ["node"] = op.Node,
                ["type"] = type.FullName,
                ["field"] = op.Field,
                ["target"] = op.Target,
            };
            if (targetTypeName != null)
                result["target_type"] = targetTypeName;
            return result;
        }

        static UnityEngine.Object ResolveReference(HierarchyTarget target, HierarchyOp op, out string targetTypeName)
        {
            targetTypeName = null;
            if (string.IsNullOrWhiteSpace(op.Target))
                throw new ArgumentException("set_reference requires target.");

            var targetPath = op.Target.Replace('\\', '/');
            if (targetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                Type assetType = null;
                if (!string.IsNullOrWhiteSpace(op.TargetType))
                {
                    assetType = ResolveType(op.TargetType);
                    targetTypeName = assetType.FullName;
                }

                var asset = assetType == null
                    ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath)
                    : AssetDatabase.LoadAssetAtPath(targetPath, assetType);
                if (asset == null)
                    throw new InvalidOperationException($"Asset not found: {targetPath}");
                return asset;
            }

            var targetNode = target.Find(op.Target);
            if (targetNode == null)
                throw new InvalidOperationException($"Target node not found: {op.Target}.");
            if (string.IsNullOrWhiteSpace(op.TargetType))
                return targetNode.gameObject;

            var componentType = ResolveComponent(op.TargetType);
            var component = targetNode.GetComponent(componentType);
            if (component == null)
                throw new InvalidOperationException($"{op.Target} has no {componentType.FullName}.");
            targetTypeName = componentType.FullName;
            return component;
        }

        static Dictionary<string, object> Rename(HierarchyOp op, Transform node)
        {
            RequirePlainName(op.Name, "rename");
            node.name = op.Name;
            return new Dictionary<string, object>
            {
                ["action"] = "rename",
                ["node"] = op.Node,
                ["name"] = op.Name,
            };
        }

        static Dictionary<string, object> DeleteNode(HierarchyTarget target, HierarchyOp op, Transform node)
        {
            if (target.IsPrefabRoot(node))
                throw new InvalidOperationException("Cannot delete the prefab root.");
            UnityEngine.Object.DestroyImmediate(node.gameObject);
            return new Dictionary<string, object>
            {
                ["action"] = "delete_node",
                ["node"] = op.Node,
            };
        }

        static Dictionary<string, object> DeleteComponent(HierarchyOp op, Transform node)
        {
            var type = ResolveComponent(op.Type);
            if (typeof(Transform).IsAssignableFrom(type))
                throw new InvalidOperationException("Transform cannot be removed.");
            var component = node.GetComponent(type);
            if (component == null)
                throw new InvalidOperationException($"{op.Node} has no {type.FullName}.");
            UnityEngine.Object.DestroyImmediate(component);
            return new Dictionary<string, object>
            {
                ["action"] = "delete_component",
                ["node"] = op.Node,
                ["type"] = type.FullName,
            };
        }

        static Dictionary<string, object> SetProperty(HierarchyOp op, Transform node)
        {
            var type = ResolveComponent(op.Type);
            var component = node.GetComponent(type);
            if (component == null)
                throw new InvalidOperationException($"{op.Node} has no {type.FullName}.");
            if (string.IsNullOrWhiteSpace(op.Field))
                throw new ArgumentException("set_property requires field.");

            var so = new SerializedObject(component);
            var prop = FindField(so, op.Field);
            if (prop == null)
                throw new InvalidOperationException($"{type.FullName} has no field {op.Field}.");
            Assign(prop, op.Value);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            return new Dictionary<string, object>
            {
                ["action"] = "set_property",
                ["node"] = op.Node,
                ["type"] = type.FullName,
                ["field"] = prop.propertyPath,
                ["value"] = ExportValue(prop),
            };
        }

        static Dictionary<string, object> SetRect(HierarchyOp op, Transform node)
        {
            if (node is not RectTransform rect)
                throw new InvalidOperationException(
                    $"{op.Node} has no RectTransform. create_node under a UI parent adds one.");

            var wrote = false;
            if (op.AnchorMin != null)
            {
                rect.anchorMin = ReadVector2(op.AnchorMin, "anchor_min");
                wrote = true;
            }

            if (op.AnchorMax != null)
            {
                rect.anchorMax = ReadVector2(op.AnchorMax, "anchor_max");
                wrote = true;
            }

            if (op.Pivot != null)
            {
                rect.pivot = ReadVector2(op.Pivot, "pivot");
                wrote = true;
            }

            if (op.AnchoredPosition != null)
            {
                rect.anchoredPosition = ReadVector2(op.AnchoredPosition, "anchored_position");
                wrote = true;
            }

            if (op.SizeDelta != null)
            {
                rect.sizeDelta = ReadVector2(op.SizeDelta, "size_delta");
                wrote = true;
            }

            if (!wrote)
                throw new ArgumentException(
                    "set_rect requires anchor_min, anchor_max, pivot, anchored_position, or size_delta.");

            EditorUtility.SetDirty(rect);
            return new Dictionary<string, object>
            {
                ["action"] = "set_rect",
                ["node"] = op.Node,
                ["rect"] = RectData(rect),
            };
        }

        static void Collect(Transform current, string path, List<Dictionary<string, object>> nodes)
        {
            nodes.Add(Summary(current, path));
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

        static Dictionary<string, int> CountNames(GameObject[] roots)
        {
            var nameCount = new Dictionary<string, int>();
            for (int i = 0; i < roots.Length; i++)
            {
                nameCount.TryGetValue(roots[i].name, out var count);
                nameCount[roots[i].name] = count + 1;
            }

            return nameCount;
        }

        static Dictionary<string, object> Summary(Transform node, string path)
        {
            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["name"] = node.name,
                ["active"] = node.gameObject.activeSelf,
                ["components"] = ComponentNames(node),
            };
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

        static Dictionary<string, object> RectData(RectTransform rect)
        {
            return new Dictionary<string, object>
            {
                ["anchor_min"] = Vector2Data(rect.anchorMin),
                ["anchor_max"] = Vector2Data(rect.anchorMax),
                ["pivot"] = Vector2Data(rect.pivot),
                ["anchored_position"] = Vector2Data(rect.anchoredPosition),
                ["size_delta"] = Vector2Data(rect.sizeDelta),
            };
        }

        static Dictionary<string, object> Vector2Data(Vector2 value)
        {
            return new Dictionary<string, object>
            {
                ["x"] = value.x,
                ["y"] = value.y,
            };
        }

        static List<Dictionary<string, object>> Fields(Transform node)
        {
            var fields = new List<Dictionary<string, object>>();
            var components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;
                AppendFields(component, fields);
            }

            return fields;
        }

        static void AppendFields(Component component, List<Dictionary<string, object>> fields)
        {
            var so = new SerializedObject(component);
            var it = so.GetIterator();
            var enter = true;
            while (it.NextVisible(enter))
            {
                if (it.depth == 0 && SkipField(it.name))
                {
                    enter = false;
                    continue;
                }

                if (it.depth > 1)
                {
                    enter = false;
                    continue;
                }

                if (it.isArray && it.propertyType != SerializedPropertyType.String)
                {
                    enter = false;
                    continue;
                }

                if (it.propertyType == SerializedPropertyType.Generic)
                {
                    enter = it.depth == 0;
                    continue;
                }

                if (!CanList(it.propertyType))
                {
                    enter = false;
                    continue;
                }

                fields.Add(new Dictionary<string, object>
                {
                    ["component"] = component.GetType().FullName,
                    ["name"] = it.propertyPath,
                    ["type"] = it.propertyType.ToString(),
                    ["value"] = ExportValue(it),
                });
                enter = false;
            }
        }

        static bool SkipField(string name)
        {
            return name == "m_Script"
                   || name == "m_ObjectHideFlags"
                   || name == "m_CorrespondingSourceObject"
                   || name == "m_PrefabInstance"
                   || name == "m_PrefabAsset"
                   || name == "m_GameObject";
        }

        static bool CanList(SerializedPropertyType type)
        {
            switch (type)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.String:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.Character:
                    return true;
                default:
                    return false;
            }
        }

        static void Assign(SerializedProperty prop, object raw)
        {
            if (prop.propertyType == SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException(
                    $"{prop.propertyPath} is an object reference. Use set_reference.");
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
                throw new InvalidOperationException(
                    $"{prop.propertyPath} is an array. Pass a scalar, string, enum, color, vector, or rect.");

            var token = ToToken(raw);
            if (token == null || token.Type == JTokenType.Null)
                throw new ArgumentException($"set_property requires value for {prop.propertyPath}.");

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = token.Value<int>();
                    return;
                case SerializedPropertyType.Float:
                    prop.floatValue = token.Value<float>();
                    return;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = token.Value<bool>();
                    return;
                case SerializedPropertyType.String:
                    prop.stringValue = token.Type == JTokenType.Null ? "" : token.Value<string>();
                    return;
                case SerializedPropertyType.Enum:
                    AssignEnum(prop, token);
                    return;
                case SerializedPropertyType.Color:
                    prop.colorValue = ReadColor(token);
                    return;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ReadVector2(token, prop.propertyPath);
                    return;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ReadVector3(token, prop.propertyPath);
                    return;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ReadVector4(token, prop.propertyPath);
                    return;
                case SerializedPropertyType.Vector2Int:
                    var v2 = ReadVector2(token, prop.propertyPath);
                    prop.vector2IntValue = new Vector2Int(Mathf.RoundToInt(v2.x), Mathf.RoundToInt(v2.y));
                    return;
                case SerializedPropertyType.Vector3Int:
                    var v3 = ReadVector3(token, prop.propertyPath);
                    prop.vector3IntValue = new Vector3Int(
                        Mathf.RoundToInt(v3.x), Mathf.RoundToInt(v3.y), Mathf.RoundToInt(v3.z));
                    return;
                case SerializedPropertyType.Rect:
                    prop.rectValue = ReadRect(token);
                    return;
                case SerializedPropertyType.Character:
                    prop.intValue = token.Type == JTokenType.String
                        ? token.Value<string>()[0]
                        : token.Value<int>();
                    return;
                default:
                    throw new InvalidOperationException(
                        $"{prop.propertyPath} has unsupported type {prop.propertyType}. Pass a scalar, string, enum, color, vector, or rect.");
            }
        }

        static void AssignEnum(SerializedProperty prop, JToken token)
        {
            if (token.Type == JTokenType.Integer)
            {
                var index = token.Value<int>();
                if (index < 0 || index >= prop.enumNames.Length)
                    throw new ArgumentException($"Enum index {index} is outside {prop.propertyPath}.");
                prop.enumValueIndex = index;
                return;
            }

            var name = token.Value<string>();
            var names = prop.enumNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                    continue;
                prop.enumValueIndex = i;
                return;
            }

            var display = prop.enumDisplayNames;
            for (int i = 0; i < display.Length; i++)
            {
                if (!string.Equals(display[i], name, StringComparison.OrdinalIgnoreCase))
                    continue;
                prop.enumValueIndex = i;
                return;
            }

            throw new ArgumentException(
                $"Unknown enum value {name} for {prop.propertyPath}. Use one of: {string.Join(", ", names)}.");
        }

        static object ExportValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Character:
                    return prop.intValue;
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length
                        ? prop.enumNames[prop.enumValueIndex]
                        : prop.enumValueIndex;
                case SerializedPropertyType.Color:
                    var color = prop.colorValue;
                    return new Dictionary<string, object>
                    {
                        ["r"] = color.r,
                        ["g"] = color.g,
                        ["b"] = color.b,
                        ["a"] = color.a,
                    };
                case SerializedPropertyType.Vector2:
                    return Vector2Data(prop.vector2Value);
                case SerializedPropertyType.Vector3:
                    return new Dictionary<string, object>
                    {
                        ["x"] = prop.vector3Value.x,
                        ["y"] = prop.vector3Value.y,
                        ["z"] = prop.vector3Value.z,
                    };
                case SerializedPropertyType.Vector4:
                    return new Dictionary<string, object>
                    {
                        ["x"] = prop.vector4Value.x,
                        ["y"] = prop.vector4Value.y,
                        ["z"] = prop.vector4Value.z,
                        ["w"] = prop.vector4Value.w,
                    };
                case SerializedPropertyType.Vector2Int:
                    return new Dictionary<string, object>
                    {
                        ["x"] = prop.vector2IntValue.x,
                        ["y"] = prop.vector2IntValue.y,
                    };
                case SerializedPropertyType.Vector3Int:
                    return new Dictionary<string, object>
                    {
                        ["x"] = prop.vector3IntValue.x,
                        ["y"] = prop.vector3IntValue.y,
                        ["z"] = prop.vector3IntValue.z,
                    };
                case SerializedPropertyType.Rect:
                    var rect = prop.rectValue;
                    return new Dictionary<string, object>
                    {
                        ["x"] = rect.x,
                        ["y"] = rect.y,
                        ["width"] = rect.width,
                        ["height"] = rect.height,
                    };
                case SerializedPropertyType.ObjectReference:
                    return AssetLabel(prop.objectReferenceValue);
                default:
                    return null;
            }
        }

        static object AssetLabel(UnityEngine.Object obj)
        {
            if (obj == null)
                return null;
            var path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? obj.name : path;
        }

        static SerializedProperty FindField(SerializedObject so, string field)
        {
            if (string.IsNullOrWhiteSpace(field))
                return null;
            var direct = so.FindProperty(field);
            if (direct != null)
                return direct;

            SerializedProperty found = null;
            var it = so.GetIterator();
            if (!it.NextVisible(true))
                return null;
            do
            {
                if (!string.Equals(it.name, field, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(it.displayName, field, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (found != null)
                    throw new InvalidOperationException(
                        $"Field {field} matches more than one property. Use the serialized name.");
                found = so.FindProperty(it.propertyPath);
            }
            while (it.NextVisible(false));

            return found;
        }

        static JToken ToToken(object raw)
        {
            if (raw == null)
                return null;
            if (raw is JToken token)
                return token;
            return JToken.FromObject(raw);
        }

        static Vector2 ReadVector2(object raw, string label)
        {
            var token = ToToken(raw);
            if (token is JArray array)
            {
                if (array.Count < 2)
                    throw new ArgumentException($"{label} needs two numbers.");
                return new Vector2(array[0].Value<float>(), array[1].Value<float>());
            }

            if (token is JObject obj && obj["x"] != null && obj["y"] != null)
                return new Vector2(obj["x"].Value<float>(), obj["y"].Value<float>());
            throw new ArgumentException($"{label} must be {{\"x\":0,\"y\":0}}.");
        }

        static Vector3 ReadVector3(JToken token, string label)
        {
            if (token is JArray array)
            {
                if (array.Count < 3)
                    throw new ArgumentException($"{label} needs three numbers.");
                return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
            }

            if (token is JObject obj && obj["x"] != null && obj["y"] != null && obj["z"] != null)
                return new Vector3(obj["x"].Value<float>(), obj["y"].Value<float>(), obj["z"].Value<float>());
            throw new ArgumentException($"{label} must be {{\"x\":0,\"y\":0,\"z\":0}}.");
        }

        static Vector4 ReadVector4(JToken token, string label)
        {
            if (token is JArray array)
            {
                if (array.Count < 4)
                    throw new ArgumentException($"{label} needs four numbers.");
                return new Vector4(
                    array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>(), array[3].Value<float>());
            }

            if (token is JObject obj && obj["x"] != null && obj["y"] != null && obj["z"] != null && obj["w"] != null)
                return new Vector4(
                    obj["x"].Value<float>(), obj["y"].Value<float>(), obj["z"].Value<float>(), obj["w"].Value<float>());
            throw new ArgumentException($"{label} must be {{\"x\":0,\"y\":0,\"z\":0,\"w\":0}}.");
        }

        static Color ReadColor(JToken token)
        {
            if (token is JArray array)
            {
                if (array.Count < 3)
                    throw new ArgumentException("color needs r, g, b, and optional a.");
                var alpha = array.Count > 3 ? array[3].Value<float>() : 1f;
                return new Color(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>(), alpha);
            }

            if (token is JObject obj && obj["r"] != null && obj["g"] != null && obj["b"] != null)
            {
                var alpha = obj["a"] == null ? 1f : obj["a"].Value<float>();
                return new Color(obj["r"].Value<float>(), obj["g"].Value<float>(), obj["b"].Value<float>(), alpha);
            }

            throw new ArgumentException("color must be {\"r\":1,\"g\":1,\"b\":1,\"a\":1}.");
        }

        static Rect ReadRect(JToken token)
        {
            if (token is JObject obj &&
                obj["x"] != null && obj["y"] != null && obj["width"] != null && obj["height"] != null)
            {
                return new Rect(
                    obj["x"].Value<float>(),
                    obj["y"].Value<float>(),
                    obj["width"].Value<float>(),
                    obj["height"].Value<float>());
            }

            throw new ArgumentException("rect must be {\"x\":0,\"y\":0,\"width\":0,\"height\":0}.");
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

        static string SegmentOf(Transform node)
        {
            int index;
            int same;
            if (node.parent == null)
            {
                var roots = node.gameObject.scene.GetRootGameObjects();
                index = 0;
                same = 0;
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].transform == node)
                        index = i;
                    if (roots[i].name == node.name)
                        same++;
                }
            }
            else
            {
                var parent = node.parent;
                index = node.GetSiblingIndex();
                same = 0;
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (parent.GetChild(i).name == node.name)
                        same++;
                }
            }

            return same > 1 ? "#" + (index + 1) : node.name;
        }

        static void RequirePlainName(string name, string action)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"{action} requires name.");
            if (name.IndexOf('/') >= 0 || IsChildIndex(name))
                throw new ArgumentException("name cannot contain '/' or be a #n index.");
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

        public static Type ResolveComponent(string name)
        {
            var type = ResolveType(name, requireComponent: true);
            if (!typeof(Component).IsAssignableFrom(type))
                throw new InvalidOperationException($"{type.FullName} is not a component.");
            if (!IsConcreteComponent(type))
                throw new InvalidOperationException($"{type.FullName} is abstract and cannot be added.");
            return type;
        }

        static Type ResolveType(string name) => ResolveType(name, requireComponent: false);

        static Type ResolveType(string name, bool requireComponent)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("type is required.");

            if (name.IndexOf('.') >= 0)
            {
                var exact = FindExact(name);
                if (exact == null)
                    throw new InvalidOperationException($"Type not found: {name}.");
                return exact;
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
                    if (candidate == null || candidate.Name != name)
                        continue;
                    if (requireComponent && !IsConcreteComponent(candidate))
                        continue;
                    if (!matches.Contains(candidate))
                        matches.Add(candidate);
                }
            }

            if (matches.Count == 0)
                throw new InvalidOperationException(
                    requireComponent
                        ? $"No component named {name}. Use a full name such as UnityEngine.UI.{name}."
                        : $"Type not found: {name}.");
            if (matches.Count == 1)
                return matches[0];

            if (requireComponent)
            {
                var ui = matches.FindAll(type => type.Namespace == "UnityEngine.UI");
                if (ui.Count == 1)
                    return ui[0];
            }

            throw new InvalidOperationException(
                $"Short name {name} matches more than one type: {Join(matches)}. Use a full name.");
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
    }

    public sealed class HierarchyOp
    {
        [JsonProperty("action")] public string Action { get; set; }
        [JsonProperty("node")] public string Node { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("field")] public string Field { get; set; }
        [JsonProperty("target")] public string Target { get; set; }
        [JsonProperty("target_type")] public string TargetType { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("parent")] public string Parent { get; set; }
        [JsonProperty("value")] public object Value { get; set; }
        [JsonProperty("prefab_path")] public string PrefabPath { get; set; }
        [JsonProperty("sibling")] public int? Sibling { get; set; }
        [JsonProperty("anchor_min")] public object AnchorMin { get; set; }
        [JsonProperty("anchor_max")] public object AnchorMax { get; set; }
        [JsonProperty("pivot")] public object Pivot { get; set; }
        [JsonProperty("anchored_position")] public object AnchoredPosition { get; set; }
        [JsonProperty("size_delta")] public object SizeDelta { get; set; }
    }
}

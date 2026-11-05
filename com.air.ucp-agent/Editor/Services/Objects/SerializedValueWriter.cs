using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Air.UcpAgent.Cli;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Writes JSON values into Unity serialized properties. Property names are accepted in whatever
    /// form the caller knows them (<c>m_Mass</c>, <c>mass</c>, <c>useGravity</c>) and object
    /// references can be given as an asset path, a GUID, a node path, or an instance id.
    /// </summary>
    /// <remarks>
    /// Accepts exactly the shapes <see cref="SerializedValueReader"/> emits, so a caller can
    /// round-trip <c>component_get</c> → edit → <c>component_set</c>. Value coercion lives in
    /// <see cref="JsonValues"/>.
    /// </remarks>
    public static class SerializedValueWriter
    {
        /// <summary>Applies every entry, returning the property paths actually written.</summary>
        /// <param name="scope">
        /// The target the object lives in: resolves <c>{"node": ..}</c> references and decides
        /// whether the write records undo. Null when there is no such target.
        /// </param>
        public static List<string> Apply(Object target, JObject properties, EditTargetScope scope)
        {
            if (target == null)
                throw new ArgumentException("Cannot set properties on a missing object.");

            var applied = new List<string>();
            if (properties == null || properties.Count == 0)
                return applied;

            var serialized = new SerializedObject(target);
            var viaReflection = new List<KeyValuePair<string, JToken>>();

            foreach (var pair in properties)
            {
                var property = FindProperty(serialized, pair.Key);
                if (property == null)
                {
                    viaReflection.Add(new KeyValuePair<string, JToken>(pair.Key, pair.Value));
                    continue;
                }

                Write(property, pair.Value, scope);
                applied.Add(property.propertyPath);
            }

            if (applied.Count > 0)
            {
                if (scope != null && scope.SupportsUndo)
                    serialized.ApplyModifiedProperties();
                else
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var pair in viaReflection)
            {
                if (!TryWriteViaReflection(target, pair.Key, pair.Value, scope))
                {
                    throw new ArgumentException(
                        $"'{target.GetType().Name}' has no settable property '{pair.Key}'. " +
                        $"Settable names: {DescribeSettable(target)}");
                }

                applied.Add(pair.Key);
                EditorUtility.SetDirty(target);
            }

            return applied;
        }

        /// <summary>
        /// Resolves a property name the way a human writes it: exact serialized name, then the
        /// <c>m_Pascal</c> backing-field convention, then a case-insensitive scan.
        /// </summary>
        public static SerializedProperty FindProperty(SerializedObject serialized, string name)
        {
            if (serialized == null || string.IsNullOrWhiteSpace(name))
                return null;

            var trimmed = name.Trim();

            var direct = serialized.FindProperty(trimmed);
            if (direct != null)
                return direct;

            var backing = serialized.FindProperty("m_" + Capitalize(trimmed));
            if (backing != null)
                return backing;

            var iterator = serialized.GetIterator();
            if (!iterator.NextVisible(true))
                return null;

            do
            {
                if (NamesMatch(iterator.name, trimmed))
                    return serialized.FindProperty(iterator.propertyPath);
            }
            while (iterator.NextVisible(false));

            return null;
        }

        /// <summary>Same name resolution, applied to a child of an existing property.</summary>
        public static SerializedProperty FindRelative(SerializedProperty parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
                return null;

            var trimmed = name.Trim();
            return parent.FindPropertyRelative(trimmed)
                   ?? parent.FindPropertyRelative("m_" + Capitalize(trimmed));
        }

        public static void Write(SerializedProperty property, JToken value, EditTargetScope scope)
        {
            if (property == null)
                throw new ArgumentException("Cannot write to a missing property.");

            var where = property.propertyPath;
            RequireNotTruncated(value, where);

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (IsLongProperty(property))
                        property.longValue = JsonValues.ToLong(value, where);
                    else
                        property.intValue = (int)JsonValues.ToLong(value, where);
                    return;

                case SerializedPropertyType.Boolean:
                    property.boolValue = JsonValues.ToBool(value, where);
                    return;

                case SerializedPropertyType.Float:
                    if (string.Equals(property.type, "double", StringComparison.Ordinal))
                        property.doubleValue = JsonValues.ToDouble(value, where);
                    else
                        property.floatValue = (float)JsonValues.ToDouble(value, where);
                    return;

                case SerializedPropertyType.String:
                    property.stringValue = JsonValues.IsAbsent(value) ? string.Empty : value.ToObject<string>();
                    return;

                case SerializedPropertyType.Character:
                    property.intValue = ToChar(value, where);
                    return;

                case SerializedPropertyType.Color:
                    property.colorValue = JsonValues.ToColor(value, where);
                    return;

                case SerializedPropertyType.Enum:
                    property.enumValueIndex = ToEnumIndex(value, property);
                    return;

                case SerializedPropertyType.LayerMask:
                    property.intValue = ToLayerMask(value, where);
                    return;

                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = ResolveReference(value, ExpectedReferenceType(property), scope);
                    return;

                case SerializedPropertyType.Vector2:
                    property.vector2Value = JsonValues.ToVector2(value, where);
                    return;

                case SerializedPropertyType.Vector3:
                    property.vector3Value = JsonValues.ToVector3(value, where);
                    return;

                case SerializedPropertyType.Vector4:
                    property.vector4Value = JsonValues.ToVector4(value, where);
                    return;

                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = JsonValues.ToQuaternion(value, where);
                    return;

                case SerializedPropertyType.Vector2Int:
                {
                    var v = JsonValues.ToFloats(value, 2, where);
                    property.vector2IntValue = new Vector2Int((int)v[0], (int)v[1]);
                    return;
                }

                case SerializedPropertyType.Vector3Int:
                {
                    var v = JsonValues.ToFloats(value, 3, where);
                    property.vector3IntValue = new Vector3Int((int)v[0], (int)v[1], (int)v[2]);
                    return;
                }

                case SerializedPropertyType.Rect:
                {
                    var v = JsonValues.ToFloats(value, 4, where, "x", "y", "width", "height");
                    property.rectValue = new Rect(v[0], v[1], v[2], v[3]);
                    return;
                }

                case SerializedPropertyType.RectInt:
                {
                    var v = JsonValues.ToFloats(value, 4, where, "x", "y", "width", "height");
                    property.rectIntValue = new RectInt((int)v[0], (int)v[1], (int)v[2], (int)v[3]);
                    return;
                }

                case SerializedPropertyType.Bounds:
                {
                    var (center, size) = JsonValues.ToBoundsParts(value, where);
                    property.boundsValue = new Bounds(center, size);
                    return;
                }

                case SerializedPropertyType.BoundsInt:
                {
                    var (center, size) = JsonValues.ToBoundsParts(value, where);
                    property.boundsIntValue = new BoundsInt(
                        new Vector3Int((int)center.x, (int)center.y, (int)center.z),
                        new Vector3Int((int)size.x, (int)size.y, (int)size.z));
                    return;
                }

                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = JsonValues.ToCurve(value, where);
                    return;

                case SerializedPropertyType.ArraySize:
                    property.intValue = (int)JsonValues.ToLong(value, where);
                    return;

                case SerializedPropertyType.Generic:
                    WriteGeneric(property, value, scope);
                    return;

                default:
                    throw new ArgumentException(
                        $"Property '{where}' has unsupported type {property.propertyType}. Use the 'exec' command for this one.");
            }
        }

        static void RequireNotTruncated(JToken value, string where)
        {
            if (value is JObject obj && obj.ContainsKey(SerializedValueReader.TruncatedKey))
            {
                throw new ArgumentException(
                    $"Value for '{where}' is a truncated read ({obj[SerializedValueReader.TruncatedHintKey]}); read it again in full before writing it back.");
            }
        }

        static void WriteGeneric(SerializedProperty property, JToken value, EditTargetScope scope)
        {
            if (property.isArray)
            {
                if (value is not JArray array)
                {
                    throw new ArgumentException(
                        $"Property '{property.propertyPath}' is an array; value must be a JSON array.");
                }

                // Checked before resizing: writing a truncated read back would silently drop the tail.
                foreach (var element in array)
                    RequireNotTruncated(element, property.propertyPath);

                property.arraySize = array.Count;
                for (var i = 0; i < array.Count; i++)
                    Write(property.GetArrayElementAtIndex(i), array[i], scope);

                return;
            }

            if (value is not JObject nested)
            {
                throw new ArgumentException(
                    $"Property '{property.propertyPath}' is a struct/class; value must be a JSON object of its fields.");
            }

            foreach (var pair in nested)
            {
                var child = FindRelative(property, pair.Key);
                if (child == null)
                {
                    throw new ArgumentException(
                        $"'{property.propertyPath}' has no field '{pair.Key}'. Fields: {DescribeChildren(property)}");
                }

                Write(child, pair.Value, scope);
            }
        }

        // --- object references --------------------------------------------------------------

        /// <summary>
        /// Resolves an object reference from an asset path, a 32-char GUID, a node path, an instance
        /// id, or an object form carrying any of those keys.
        /// </summary>
        public static Object ResolveReference(JToken value, Type expectedType, EditTargetScope scope)
        {
            expectedType ??= typeof(Object);

            if (JsonValues.IsAbsent(value))
                return null;

            if (value is JObject form)
            {
                var assetPath = JsonValues.Text(form, "asset", "asset_path", ParamKeys.Path);
                if (!string.IsNullOrWhiteSpace(assetPath))
                    return LoadFromPath(assetPath, JsonValues.Text(form, "sub_asset", "sub"), expectedType);

                var guid = JsonValues.Text(form, "guid");
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                        throw new ArgumentException($"No asset for guid '{guid}'.");

                    return LoadFromPath(path, JsonValues.Text(form, "sub_asset", "sub"), expectedType);
                }

                var builtin = JsonValues.Text(form, "builtin", "builtin_resource");
                if (!string.IsNullOrWhiteSpace(builtin))
                    return LoadBuiltin(builtin, expectedType);

                var node = JsonValues.Text(form, "node", "gameobject", "go");
                if (!string.IsNullOrWhiteSpace(node))
                    return ResolveNodeReference(node, JsonValues.Text(form, "component", "type"), expectedType, scope);

                var id = JsonValues.Pick(form, "id", "instance_id");
                if (!JsonValues.IsAbsent(id))
                    return FromInstanceId(id.ToObject<int>(), expectedType);

                throw new ArgumentException(
                    "Object reference must carry one of: asset, guid, builtin, node, id. Got: " +
                    string.Join(", ", form.Properties().Select(p => p.Name)));
            }

            if (value.Type == JTokenType.Integer)
                return FromInstanceId(value.ToObject<int>(), expectedType);

            var text = value.ToObject<string>();
            if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (text.StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase))
                return LoadBuiltin(text.Substring(BuiltinPrefix.Length), expectedType);

            if (LooksLikeAssetPath(text))
                return LoadFromPath(text, null, expectedType);

            if (LooksLikeGuid(text))
            {
                var path = AssetDatabase.GUIDToAssetPath(text);
                if (!string.IsNullOrEmpty(path))
                    return LoadFromPath(path, null, expectedType);
            }

            return ResolveNodeReference(text, null, expectedType, scope);
        }

        const string BuiltinPrefix = "builtin:";

        /// <summary>
        /// Unity's own default assets have no path under Assets/ and no GUID a caller can discover,
        /// so they need their own addressing form: <c>{"builtin": "LegacyRuntime.ttf"}</c> or
        /// <c>"builtin:UI/Skin/UISprite.psd"</c>. Without this a UI built from scratch has no font
        /// and renders nothing, which is the first wall anyone hits.
        /// </summary>
        static Object LoadBuiltin(string path, Type expectedType)
        {
            var trimmed = path.Trim();

            // UI defaults live in the editor's "builtin extra" bundle; engine defaults come from
            // UnityEngine.Resources, which is a different lookup.
            var asset = AssetDatabase.GetBuiltinExtraResource(expectedType, trimmed)
                        ?? Resources.GetBuiltinResource(expectedType, trimmed);
            if (asset != null)
                return asset;

            throw new ArgumentException(
                $"No builtin {expectedType.Name} named '{trimmed}'. Common ones: " +
                "'LegacyRuntime.ttf' (Font), 'UI/Skin/UISprite.psd' and 'UI/Skin/InputFieldBackground.psd' (Sprite), " +
                "'Sprites-Default.mat' and 'Default-Material' (Material).");
        }

        static Object LoadFromPath(string path, string subAssetName, Type expectedType)
        {
            var normalized = AssetPaths.RequireExisting(path, "asset");

            if (!string.IsNullOrWhiteSpace(subAssetName))
            {
                var sub = AssetDatabase.LoadAllAssetsAtPath(normalized)
                    .FirstOrDefault(a =>
                        a != null &&
                        string.Equals(a.name, subAssetName, StringComparison.Ordinal) &&
                        expectedType.IsInstanceOfType(a));
                if (sub == null)
                {
                    throw new ArgumentException(
                        $"'{normalized}' has no sub-asset '{subAssetName}' of type {expectedType.Name}.");
                }

                return sub;
            }

            var asset = AssetDatabase.LoadAssetAtPath(normalized, expectedType);
            if (asset != null)
                return asset;

            // A component-typed field pointing at a prefab means "that component on the prefab root".
            if (typeof(Component).IsAssignableFrom(expectedType))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(normalized);
                var component = go == null ? null : go.GetComponent(expectedType);
                if (component != null)
                    return component;
            }

            var any = AssetDatabase.LoadAllAssetsAtPath(normalized)
                .FirstOrDefault(a => a != null && expectedType.IsInstanceOfType(a));
            if (any != null)
                return any;

            throw new ArgumentException($"'{normalized}' holds no {expectedType.Name}.");
        }

        static Object ResolveNodeReference(string nodePath, string componentType, Type expectedType, EditTargetScope scope)
        {
            if (scope == null)
            {
                throw new ArgumentException(
                    $"Cannot resolve node reference '{nodePath}' without an edit target. Use an asset path or guid instead.");
            }

            var node = GameObjectPath.Require(scope, nodePath, ParamKeys.Node);

            if (!string.IsNullOrWhiteSpace(componentType))
            {
                var type = ComponentTypeResolver.RequireComponent(componentType);
                var component = node.GetComponent(type);
                if (component == null)
                    throw new UcpException(UcpErrorCodes.ComponentNotFound, $"Node '{nodePath}' has no {type.Name}.");

                return component;
            }

            if (expectedType == typeof(GameObject) || expectedType == typeof(Object))
                return node.gameObject;

            if (typeof(Component).IsAssignableFrom(expectedType))
            {
                var component = node.GetComponent(expectedType);
                if (component == null)
                    throw new UcpException(UcpErrorCodes.ComponentNotFound, $"Node '{nodePath}' has no {expectedType.Name}.");

                return component;
            }

            return node.gameObject;
        }

        static Object FromInstanceId(int id, Type expectedType)
        {
            var obj = EditorUtility.InstanceIDToObject(id);
            if (obj == null)
                throw new ArgumentException($"No object with instance id {id}.");

            if (expectedType.IsInstanceOfType(obj))
                return obj;

            if (obj is GameObject go && typeof(Component).IsAssignableFrom(expectedType))
            {
                var component = go.GetComponent(expectedType);
                if (component != null)
                    return component;
            }

            throw new ArgumentException($"Instance {id} is a {obj.GetType().Name}, not a {expectedType.Name}.");
        }

        /// <summary>Unity encodes reference field types as <c>PPtr&lt;$Material&gt;</c>.</summary>
        public static Type ExpectedReferenceType(SerializedProperty property)
        {
            var type = property?.type;
            if (type != null &&
                type.StartsWith("PPtr<$", StringComparison.Ordinal) &&
                type.EndsWith(">", StringComparison.Ordinal))
            {
                var name = type.Substring(6, type.Length - 7);
                if (ComponentTypeResolver.TryResolveObject(name, out var resolved))
                    return resolved;
            }

            return typeof(Object);
        }

        static bool LooksLikeAssetPath(string text) =>
            text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Packages\\", StringComparison.OrdinalIgnoreCase);

        static bool LooksLikeGuid(string text) => text.Length == 32 && text.All(Uri.IsHexDigit);

        // --- reflection fallback -----------------------------------------------------------

        const BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy;

        static bool TryWriteViaReflection(Object target, string name, JToken value, EditTargetScope scope)
        {
            var type = target.GetType();

            var property = type.GetProperty(name, MemberFlags);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(target, Convert(property.PropertyType, value, scope, name));
                return true;
            }

            var field = type.GetField(name, MemberFlags);
            if (field != null && !field.IsLiteral && !field.IsInitOnly)
            {
                field.SetValue(target, Convert(field.FieldType, value, scope, name));
                return true;
            }

            return false;
        }

        static object Convert(Type type, JToken value, EditTargetScope scope, string name)
        {
            if (JsonValues.IsAbsent(value))
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            if (typeof(Object).IsAssignableFrom(type))
                return ResolveReference(value, type, scope);

            var target = Nullable.GetUnderlyingType(type) ?? type;

            if (target.IsEnum)
            {
                return value.Type == JTokenType.Integer
                    ? Enum.ToObject(target, value.ToObject<long>())
                    : Enum.Parse(target, value.ToObject<string>(), ignoreCase: true);
            }

            if (target == typeof(Vector2))
                return JsonValues.ToVector2(value, name);
            if (target == typeof(Vector3))
                return JsonValues.ToVector3(value, name);
            if (target == typeof(Vector4))
                return JsonValues.ToVector4(value, name);
            if (target == typeof(Quaternion))
                return JsonValues.ToQuaternion(value, name);
            if (target == typeof(Color))
                return JsonValues.ToColor(value, name);
            if (target == typeof(Color32))
                return (Color32)JsonValues.ToColor(value, name);

            try
            {
                return value.ToObject(type);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Cannot convert value for '{name}' to {type.Name}: {ex.Message}");
            }
        }

        // --- property-specific coercion ----------------------------------------------------

        static int ToChar(JToken value, string where)
        {
            if (value?.Type == JTokenType.Integer)
                return value.ToObject<int>();

            var text = value?.ToObject<string>();
            if (!string.IsNullOrEmpty(text))
                return text[0];

            throw JsonValues.Bad(value, where, "a character");
        }

        static int ToEnumIndex(JToken value, SerializedProperty property)
        {
            var names = property.enumNames ?? Array.Empty<string>();

            if (value?.Type == JTokenType.Integer)
            {
                var index = value.ToObject<int>();
                if (index >= 0 && index < names.Length)
                    return index;

                throw new ArgumentException(
                    $"Enum index {index} out of range for '{property.propertyPath}'. Values: {string.Join(", ", names)}.");
            }

            var text = value?.ToObject<string>();
            if (!string.IsNullOrWhiteSpace(text))
            {
                for (var i = 0; i < names.Length; i++)
                {
                    if (string.Equals(names[i], text, StringComparison.OrdinalIgnoreCase))
                        return i;
                }

                var display = property.enumDisplayNames ?? Array.Empty<string>();
                for (var i = 0; i < display.Length; i++)
                {
                    if (string.Equals(display[i], text, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            throw new ArgumentException(
                $"'{text}' is not a value of '{property.propertyPath}'. Values: {string.Join(", ", names)}.");
        }

        static int ToLayerMask(JToken value, string where)
        {
            if (value?.Type == JTokenType.Integer)
                return value.ToObject<int>();

            var names = value is JArray array
                ? array.Select(t => t.ToObject<string>()).ToArray()
                : new[] { value?.ToObject<string>() };

            var mask = 0;
            foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var layer = LayerMask.NameToLayer(name.Trim());
                if (layer < 0)
                    throw new ArgumentException($"Unknown layer '{name}' for '{where}'.");

                mask |= 1 << layer;
            }

            return mask;
        }

        // --- diagnostics -------------------------------------------------------------------

        static bool IsLongProperty(SerializedProperty property) =>
            property.type is "long" or "ulong" or "Int64" or "UInt64";

        static string Capitalize(string name) =>
            string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

        /// <summary>Compares names ignoring the <c>m_</c> prefix, underscores and case.</summary>
        static bool NamesMatch(string serializedName, string wanted) =>
            string.Equals(Canonical(serializedName), Canonical(wanted), StringComparison.OrdinalIgnoreCase);

        static string Canonical(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var trimmed = name.StartsWith("m_", StringComparison.Ordinal) ? name.Substring(2) : name;
            return trimmed.Replace("_", string.Empty);
        }

        static string DescribeChildren(SerializedProperty property)
        {
            var names = new List<string>();
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, end))
                        break;

                    names.Add(iterator.name);
                }
                while (iterator.NextVisible(false) && names.Count < 40);
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names);
        }

        static string DescribeSettable(Object target)
        {
            var names = new List<string>();
            var iterator = new SerializedObject(target).GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    names.Add(iterator.name);
                }
                while (iterator.NextVisible(false) && names.Count < 40);
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Dumps Unity serialized properties as JSON-friendly values, in the same shape
    /// <see cref="SerializedValueWriter"/> accepts — so <c>component_get</c> output can be edited
    /// and fed straight back into <c>component_set</c>.
    /// </summary>
    public static class SerializedValueReader
    {
        /// <summary>
        /// Bookkeeping fields every Unity object carries. They are never what a caller is looking
        /// for and they crowd out the real fields, so they are hidden unless asked for.
        /// </summary>
        static readonly HashSet<string> Internal = new(StringComparer.Ordinal)
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_EditorHideFlags",
            "m_EditorClassIdentifier",
            "m_Script",
        };

        public sealed class Options
        {
            /// <summary>Nesting levels of structs/arrays to expand. 0 keeps only scalars.</summary>
            public int MaxDepth { get; set; } = 2;

            /// <summary>Array elements kept before the tail is summarised.</summary>
            public int MaxArrayElements { get; set; } = 64;

            /// <summary>Include Unity's bookkeeping fields (<c>m_ObjectHideFlags</c> and friends).</summary>
            public bool IncludeInternal { get; set; }
        }

        /// <summary>Reads every visible top-level property of <paramref name="target"/>.</summary>
        public static Dictionary<string, object> Read(Object target, Options options = null)
        {
            if (target == null)
                throw new ArgumentException("Cannot read properties of a missing object.");

            options ??= new Options();
            var result = new Dictionary<string, object>();

            var serialized = new SerializedObject(target);
            var iterator = serialized.GetIterator();
            if (!iterator.NextVisible(true))
                return result;

            do
            {
                if (!options.IncludeInternal && Internal.Contains(iterator.name))
                    continue;

                result[iterator.name] = ReadProperty(iterator, options, 0);
            }
            while (iterator.NextVisible(false));

            return result;
        }

        public static object ReadProperty(SerializedProperty property, Options options, int depth)
        {
            options ??= new Options();

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.longValue;

                case SerializedPropertyType.Boolean:
                    return property.boolValue;

                case SerializedPropertyType.Float:
                    return string.Equals(property.type, "double", StringComparison.Ordinal)
                        ? property.doubleValue
                        : property.floatValue;

                case SerializedPropertyType.String:
                    return property.stringValue;

                case SerializedPropertyType.Character:
                    return property.intValue;

                case SerializedPropertyType.Color:
                {
                    var c = property.colorValue;
                    return new[] { c.r, c.g, c.b, c.a };
                }

                case SerializedPropertyType.Enum:
                {
                    var names = property.enumNames;
                    var index = property.enumValueIndex;
                    return names != null && index >= 0 && index < names.Length ? names[index] : index;
                }

                case SerializedPropertyType.LayerMask:
                    return property.intValue;

                case SerializedPropertyType.ObjectReference:
                    return Describe(property.objectReferenceValue);

                case SerializedPropertyType.Vector2:
                {
                    var v = property.vector2Value;
                    return new[] { v.x, v.y };
                }

                case SerializedPropertyType.Vector3:
                {
                    var v = property.vector3Value;
                    return new[] { v.x, v.y, v.z };
                }

                case SerializedPropertyType.Vector4:
                {
                    var v = property.vector4Value;
                    return new[] { v.x, v.y, v.z, v.w };
                }

                case SerializedPropertyType.Quaternion:
                {
                    var q = property.quaternionValue;
                    return new[] { q.x, q.y, q.z, q.w };
                }

                case SerializedPropertyType.Vector2Int:
                {
                    var v = property.vector2IntValue;
                    return new[] { v.x, v.y };
                }

                case SerializedPropertyType.Vector3Int:
                {
                    var v = property.vector3IntValue;
                    return new[] { v.x, v.y, v.z };
                }

                case SerializedPropertyType.Rect:
                {
                    var r = property.rectValue;
                    return new Dictionary<string, object>
                    {
                        ["x"] = r.x, ["y"] = r.y, ["width"] = r.width, ["height"] = r.height,
                    };
                }

                case SerializedPropertyType.RectInt:
                {
                    var r = property.rectIntValue;
                    return new Dictionary<string, object>
                    {
                        ["x"] = r.x, ["y"] = r.y, ["width"] = r.width, ["height"] = r.height,
                    };
                }

                case SerializedPropertyType.Bounds:
                {
                    var b = property.boundsValue;
                    return new Dictionary<string, object>
                    {
                        ["center"] = new[] { b.center.x, b.center.y, b.center.z },
                        ["size"] = new[] { b.size.x, b.size.y, b.size.z },
                    };
                }

                case SerializedPropertyType.BoundsInt:
                {
                    var b = property.boundsIntValue;
                    return new Dictionary<string, object>
                    {
                        ["center"] = new[] { b.center.x, b.center.y, b.center.z },
                        ["size"] = new[] { b.size.x, b.size.y, b.size.z },
                    };
                }

                case SerializedPropertyType.AnimationCurve:
                {
                    var curve = property.animationCurveValue;
                    var keys = new List<object>();
                    foreach (var key in curve?.keys ?? Array.Empty<Keyframe>())
                    {
                        keys.Add(new Dictionary<string, object>
                        {
                            ["time"] = key.time,
                            ["value"] = key.value,
                            ["inTangent"] = key.inTangent,
                            ["outTangent"] = key.outTangent,
                        });
                    }

                    return keys;
                }

                case SerializedPropertyType.ArraySize:
                    return property.intValue;

                case SerializedPropertyType.Generic:
                    return ReadGeneric(property, options, depth);

                default:
                    return $"<unreadable: {property.propertyType}>";
            }
        }

        static object ReadGeneric(SerializedProperty property, Options options, int depth)
        {
            if (property.isArray)
            {
                if (depth >= options.MaxDepth)
                    return Truncated($"{property.arrayElementType}[{property.arraySize}]");

                var kept = Math.Min(property.arraySize, Math.Max(0, options.MaxArrayElements));
                var elements = new List<object>(kept + 1);
                for (var i = 0; i < kept; i++)
                    elements.Add(ReadProperty(property.GetArrayElementAtIndex(i), options, depth + 1));

                if (property.arraySize > kept)
                    elements.Add(Truncated(
                        $"{property.arrayElementType}[{property.arraySize}]",
                        $"{property.arraySize - kept} more; raise 'max_array' to read them all"));

                return elements;
            }

            if (depth >= options.MaxDepth)
                return Truncated(property.type);

            var result = new Dictionary<string, object>();
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            if (!iterator.NextVisible(true))
                return result;

            do
            {
                if (SerializedProperty.EqualContents(iterator, end))
                    break;

                result[iterator.name] = ReadProperty(iterator, options, depth + 1);
            }
            while (iterator.NextVisible(false));

            return result;
        }

        /// <summary>Key that marks a value the read did not fully expand; the writer refuses it.</summary>
        public const string TruncatedKey = "__truncated";
        public const string TruncatedTypeKey = "__type";
        public const string TruncatedHintKey = "__hint";

        static Dictionary<string, object> Truncated(string type, string hint = "raise 'depth' to expand") =>
            new Dictionary<string, object>
            {
                [TruncatedTypeKey] = type,
                [TruncatedKey] = true,
                [TruncatedHintKey] = hint,
            };

        /// <summary>Stable identity block for an object reference, shared by reads and info commands.</summary>
        public static Dictionary<string, object> Describe(Object obj)
        {
            if (obj == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(obj);
            var result = new Dictionary<string, object>
            {
                ["type"] = obj.GetType().Name,
                ["name"] = obj.name,
                ["instance_id"] = obj.GetInstanceID(),
            };

            if (!string.IsNullOrEmpty(assetPath))
            {
                result["asset_path"] = assetPath;
                result["guid"] = AssetDatabase.AssetPathToGUID(assetPath);
            }
            else if (obj is Component component)
            {
                result["node"] = GameObjectPath.Of(component.transform);
            }
            else if (obj is GameObject go)
            {
                result["node"] = GameObjectPath.Of(go.transform);
            }

            return result;
        }
    }
}

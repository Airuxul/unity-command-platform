using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Coercion from loose JSON into Unity value types, shared by command parameters and
    /// <see cref="SerializedValueWriter"/> so both accept exactly the same forms:
    /// <c>[1,2,3]</c>, <c>{"x":1,"y":2,"z":3}</c>, or a single number broadcast to every component.
    /// </summary>
    public static class JsonValues
    {
        public static bool IsAbsent(JToken token) => token == null || token.Type == JTokenType.Null;

        public static long ToLong(JToken token, string where)
        {
            switch (token?.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return token.ToObject<long>();
                case JTokenType.Boolean:
                    return token.ToObject<bool>() ? 1 : 0;
                case JTokenType.String:
                    if (long.TryParse(token.ToObject<string>(), NumberStyles.Any, CultureInfo.InvariantCulture,
                            out var parsed))
                    {
                        return parsed;
                    }

                    break;
            }

            throw Bad(token, where, "an integer");
        }

        public static double ToDouble(JToken token, string where)
        {
            switch (token?.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return token.ToObject<double>();
                case JTokenType.Boolean:
                    return token.ToObject<bool>() ? 1d : 0d;
                case JTokenType.String:
                    if (double.TryParse(token.ToObject<string>(), NumberStyles.Any, CultureInfo.InvariantCulture,
                            out var parsed))
                    {
                        return parsed;
                    }

                    break;
            }

            throw Bad(token, where, "a number");
        }

        public static bool ToBool(JToken token, string where)
        {
            switch (token?.Type)
            {
                case JTokenType.Boolean:
                    return token.ToObject<bool>();
                case JTokenType.Integer:
                case JTokenType.Float:
                    return Math.Abs(token.ToObject<double>()) > double.Epsilon;
                case JTokenType.String:
                    var text = token.ToObject<string>();
                    if (bool.TryParse(text, out var parsed))
                        return parsed;
                    if (text == "1")
                        return true;
                    if (text == "0")
                        return false;
                    break;
            }

            throw Bad(token, where, "a boolean");
        }

        /// <summary>
        /// Reads <paramref name="count"/> floats from an array, an object keyed by
        /// <paramref name="keys"/> (default x/y/z/w), or a single number broadcast to all components.
        /// </summary>
        public static float[] ToFloats(JToken token, int count, string where, params string[] keys)
        {
            var names = keys != null && keys.Length >= count ? keys : new[] { "x", "y", "z", "w" };

            if (token is JArray array)
            {
                if (array.Count < count)
                    throw Bad(token, where, $"{count} numbers (got {array.Count})");

                return Enumerable.Range(0, count).Select(i => array[i].ToObject<float>()).ToArray();
            }

            if (token is JObject form)
            {
                var result = new float[count];
                for (var i = 0; i < count; i++)
                    result[i] = Num(form, 0f, names[i]);

                return result;
            }

            if (token?.Type is JTokenType.Integer or JTokenType.Float)
                return Enumerable.Repeat(token.ToObject<float>(), count).ToArray();

            throw Bad(token, where, $"{count} numbers as an array, an object, or a single number");
        }

        public static Vector2 ToVector2(JToken token, string where)
        {
            var v = ToFloats(token, 2, where);
            return new Vector2(v[0], v[1]);
        }

        public static Vector3 ToVector3(JToken token, string where)
        {
            var v = ToFloats(token, 3, where);
            return new Vector3(v[0], v[1], v[2]);
        }

        public static Vector4 ToVector4(JToken token, string where)
        {
            var v = ToFloats(token, 4, where);
            return new Vector4(v[0], v[1], v[2], v[3]);
        }

        /// <summary>Three components are read as euler angles, four as a raw quaternion.</summary>
        public static Quaternion ToQuaternion(JToken token, string where)
        {
            if (ComponentCount(token) == 3)
            {
                var e = ToFloats(token, 3, where);
                return Quaternion.Euler(e[0], e[1], e[2]);
            }

            var q = ToFloats(token, 4, where);
            return new Quaternion(q[0], q[1], q[2], q[3]);
        }

        public static Color ToColor(JToken token, string where)
        {
            if (token is JArray array)
            {
                var v = array.Select(t => t.ToObject<float>()).ToArray();
                if (v.Length == 3)
                    return new Color(v[0], v[1], v[2]);
                if (v.Length >= 4)
                    return new Color(v[0], v[1], v[2], v[3]);

                throw Bad(token, where, "a 3- or 4-element colour array");
            }

            if (token is JObject form)
                return new Color(Num(form, 0f, "r"), Num(form, 0f, "g"), Num(form, 0f, "b"), Num(form, 1f, "a"));

            var text = token?.ToObject<string>();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var html = text.StartsWith("#", StringComparison.Ordinal) ? text : "#" + text;
                if (ColorUtility.TryParseHtmlString(html, out var parsed))
                    return parsed;
            }

            throw Bad(token, where, "a colour (#RRGGBB, [r,g,b,a] or {r,g,b,a})");
        }

        public static (Vector3 center, Vector3 size) ToBoundsParts(JToken token, string where)
        {
            if (token is JObject form)
            {
                var center = form["center"] ?? form["Center"];
                var size = form["size"] ?? form["Size"] ?? form["extents"];
                if (center != null && size != null)
                    return (ToVector3(center, where), ToVector3(size, where));
            }

            if (token is JArray array && array.Count >= 6)
            {
                var v = array.Select(t => t.ToObject<float>()).ToArray();
                return (new Vector3(v[0], v[1], v[2]), new Vector3(v[3], v[4], v[5]));
            }

            throw Bad(token, where, "bounds as {center:[x,y,z], size:[x,y,z]} or a 6-element array");
        }

        public static AnimationCurve ToCurve(JToken token, string where)
        {
            if (token is not JArray array)
                throw Bad(token, where, "an array of {time, value} keyframes");

            var keys = array.Select(element =>
            {
                if (element is JObject key)
                {
                    return new Keyframe(
                        Num(key, 0f, "time", "t"),
                        Num(key, 0f, "value", "v"),
                        Num(key, 0f, "inTangent", "in"),
                        Num(key, 0f, "outTangent", "out"));
                }

                if (element is JArray pair && pair.Count >= 2)
                    return new Keyframe(pair[0].ToObject<float>(), pair[1].ToObject<float>());

                throw Bad(element, where, "a keyframe {time, value}");
            }).ToArray();

            return new AnimationCurve(keys);
        }

        public static int ComponentCount(JToken token) =>
            token switch
            {
                JArray array => array.Count,
                JObject form => form.Count,
                _ => 1,
            };

        /// <summary>First present key wins; comparison is case-insensitive.</summary>
        public static float Num(JObject form, float fallback, params string[] keys)
        {
            var token = Pick(form, keys);
            return IsAbsent(token) ? fallback : token.ToObject<float>();
        }

        public static string Text(JObject form, params string[] keys)
        {
            var token = Pick(form, keys);
            return IsAbsent(token) ? null : token.ToObject<string>();
        }

        public static JToken Pick(JObject form, params string[] keys)
        {
            if (form == null)
                return null;

            foreach (var key in keys)
            {
                var token = form[key];
                if (!IsAbsent(token))
                    return token;

                var loose = form.Properties()
                    .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
                if (loose != null && !IsAbsent(loose.Value))
                    return loose.Value;
            }

            return null;
        }

        public static ArgumentException Bad(JToken token, string where, string expected)
        {
            var label = string.IsNullOrEmpty(where) ? "value" : $"'{where}'";
            var got = token?.ToString(Newtonsoft.Json.Formatting.None) ?? "null";
            return new ArgumentException($"{label} expects {expected}, got {got}.");
        }
    }
}

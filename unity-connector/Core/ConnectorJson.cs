using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityCliConnector
{
    /// <summary>HTTP command JSON via Unity.Newtonsoft.Json.</summary>
    public static class ConnectorJson
    {
        private static readonly JsonSerializerSettings SerializeSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(object value) =>
            JsonConvert.SerializeObject(value, SerializeSettings);

        public static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (JToken.Parse(json) is not JObject obj)
                    return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                return ToDictionary(obj);
            }
            catch (JsonException)
            {
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static Dictionary<string, object> ToDictionary(JObject obj)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in obj.Properties())
                dict[prop.Name] = ToValue(prop.Value);
            return dict;
        }

        private static object ToValue(JToken token) =>
            token.Type switch
            {
                JTokenType.Object => ToDictionary((JObject)token),
                JTokenType.Array => ToList((JArray)token),
                JTokenType.Integer => token.Value<long>(),
                JTokenType.Float => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                JTokenType.Null or JTokenType.Undefined => null,
                _ => token.Value<string>() ?? token.ToString(),
            };

        private static List<object> ToList(JArray array)
        {
            var list = new List<object>(array.Count);
            foreach (var item in array)
                list.Add(ToValue(item));
            return list;
        }

        public static object Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonConvert.DeserializeObject(json);
        }
    }
}

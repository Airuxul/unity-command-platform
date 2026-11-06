using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Air.UcpAgent
{
    public static class UcpJson
    {
        static readonly JsonSerializerSettings Settings = new()
        {
            Converters = { new StringEnumConverter() },
        };

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);

        public static object Deserialize(string json) =>
            string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject(json, Settings);
    }
}

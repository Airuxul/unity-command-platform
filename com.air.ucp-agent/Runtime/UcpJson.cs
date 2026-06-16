using Newtonsoft.Json;

namespace Air.UcpAgent
{
    public static class UcpJson
    {
        public static string Serialize(object value) => JsonConvert.SerializeObject(value);

        public static object Deserialize(string json) =>
            string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject(json);
    }
}

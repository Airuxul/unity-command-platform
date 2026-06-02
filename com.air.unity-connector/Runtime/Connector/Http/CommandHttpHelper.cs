using System;
using System.Collections.Generic;
using Air.UnityConnector.Job;
using Air.UnityGameCore.Runtime.Serialization;

namespace Air.UnityConnector.Http
{
    public static class CommandHttpHelper
    {
        public static InvokeRequest ParseInvokeRequest(string body, string endpoint)
        {
            var root = JsonSerialization.ParseObject(body ?? "{}");
            var command = GetString(root, "command") ?? "";
            var dict = GetParameters(root);
            var requestId = GetString(root, "request_id");
            if (string.IsNullOrEmpty(requestId))
                requestId = Guid.NewGuid().ToString("N");

            return new InvokeRequest
            {
                Command = command,
                Parameters = dict,
                RequestId = requestId,
                Endpoint = endpoint,
            };
        }

        public static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.TryGetValue(key, out var v) || v == null)
                return null;
            return v.ToString();
        }

        public static Dictionary<string, object> GetParameters(Dictionary<string, object> root)
        {
            if (root != null && root.TryGetValue("parameters", out var raw) &&
                raw is Dictionary<string, object> dict)
                return dict;
            return new Dictionary<string, object>();
        }
    }
}

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>Transform values in and out of payloads, shared by every command that places a node.</summary>
    public static class TransformValues
    {
        /// <summary>Sets whichever local values were given and leaves the rest alone.</summary>
        public static void ApplyLocal(Transform node, JToken position, JToken rotation, JToken scale)
        {
            if (!JsonValues.IsAbsent(position))
                node.localPosition = JsonValues.ToVector3(position, "position");
            if (!JsonValues.IsAbsent(rotation))
                node.localRotation = JsonValues.ToQuaternion(rotation, "rotation");
            if (!JsonValues.IsAbsent(scale))
                node.localScale = JsonValues.ToVector3(scale, "scale");
        }

        public static Dictionary<string, object> Describe(Transform node) =>
            new Dictionary<string, object>
            {
                ["local_position"] = ToArray(node.localPosition),
                ["local_euler"] = ToArray(node.localEulerAngles),
                ["local_scale"] = ToArray(node.localScale),
                ["position"] = ToArray(node.position),
                ["euler"] = ToArray(node.eulerAngles),
                ["lossy_scale"] = ToArray(node.lossyScale),
            };

        static float[] ToArray(Vector3 value) => new[] { value.x, value.y, value.z };
    }
}

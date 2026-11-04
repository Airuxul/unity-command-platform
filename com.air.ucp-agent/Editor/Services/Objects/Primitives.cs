using System;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// The <c>primitive</c> argument, resolved in one place so a prefab root and a scene node accept
    /// exactly the same shape names.
    /// </summary>
    public static class Primitives
    {
        public const string Allowed = "empty|cube|sphere|capsule|cylinder|plane|quad";

        public static GameObject Create(string primitive)
        {
            var kind = string.IsNullOrWhiteSpace(primitive) ? "empty" : primitive.Trim().ToLowerInvariant();
            return kind switch
            {
                "empty" or "none" or "gameobject" => new GameObject("GameObject"),
                "cube" => GameObject.CreatePrimitive(PrimitiveType.Cube),
                "sphere" => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                "capsule" => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                "cylinder" => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                "plane" => GameObject.CreatePrimitive(PrimitiveType.Plane),
                "quad" => GameObject.CreatePrimitive(PrimitiveType.Quad),
                _ => throw new ArgumentException(
                    $"Unknown primitive '{primitive}'. Valid: {Allowed.Replace("|", ", ")}."),
            };
        }
    }
}

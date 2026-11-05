using System;
using System.Collections.Generic;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// The <c>primitive</c> and <c>components</c> arguments of every "make a new GameObject" command,
    /// resolved in one place so a prefab root and a scene node accept exactly the same input.
    /// </summary>
    public static class Primitives
    {
        /// <summary>The <c>AllowedValues</c> of every <c>primitive</c> parameter.</summary>
        public const string Allowed = "empty|cube|sphere|capsule|cylinder|plane|quad";

        public static GameObject Create(string primitive)
        {
            // The binder has already checked the value against the param's AllowedValues.
            var kind = string.IsNullOrWhiteSpace(primitive) ? "empty" : primitive.Trim().ToLowerInvariant();
            return kind switch
            {
                "cube" => GameObject.CreatePrimitive(PrimitiveType.Cube),
                "sphere" => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                "capsule" => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                "cylinder" => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                "plane" => GameObject.CreatePrimitive(PrimitiveType.Plane),
                "quad" => GameObject.CreatePrimitive(PrimitiveType.Quad),
                _ => new GameObject("GameObject"),
            };
        }

        /// <summary>The name a payload reports for <paramref name="primitive"/>.</summary>
        public static string Name(string primitive) =>
            string.IsNullOrWhiteSpace(primitive) ? "empty" : primitive.Trim().ToLowerInvariant();

        /// <summary>Adds each named component the node does not have yet; returns the type names.</summary>
        public static List<string> AddComponents(GameObject node, IEnumerable<string> typeNames)
        {
            var added = new List<string>();
            foreach (var typeName in typeNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(typeName))
                    continue;

                var type = ComponentTypeResolver.RequireComponent(typeName);
                if (node.GetComponent(type) == null && node.AddComponent(type) == null)
                    throw new InvalidOperationException($"Could not add {type.Name} to '{node.name}'.");

                added.Add(type.Name);
            }

            return added;
        }
    }
}

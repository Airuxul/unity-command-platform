using System.Collections.Generic;

namespace UnityCliConnector
{
    /// <summary>Legacy list entry shape; prefer <see cref="CommandCatalog"/>.</summary>
    public sealed class CommandListEntry
    {
        public string Name { get; set; }
        public string Scope { get; set; }
        public string Description { get; set; }
        public bool IsJob { get; set; }
        public string Completion { get; set; }
    }

    public static class CommandListBuilder
    {
        public static List<Dictionary<string, object>> Build()
        {
            var response = CommandCatalog.BuildResponse();
            return response.TryGetValue("commands", out var raw) && raw is List<Dictionary<string, object>> list
                ? list
                : new List<Dictionary<string, object>>();
        }
    }
}
